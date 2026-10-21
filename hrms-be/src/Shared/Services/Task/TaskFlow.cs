using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public sealed record TaskPolicy(bool Required, LeaveConfig Config);

public class TaskFlow
{
    private readonly AppDbContext _db;
    private readonly LeaveFlow _leave;
    private readonly Notifier _notify;

    public TaskFlow(AppDbContext db, LeaveFlow leave, Notifier notify)
    {
        _db = db;
        _leave = leave;
        _notify = notify;
    }

    // call AFTER the task is saved with approval = "pending"
    public async Task NotifyApproversAsync(WorkTask t)
    {
        var ids = t.Approvals.Where(a => a.Decision == "pending").Select(a => a.ApproverUserId).Distinct().ToList();
        if (ids.Count == 0) return;

        var who = await _db.Users.AsNoTracking().Where(u => u.Id == t.UserId).Select(u => u.Name).FirstAsync();
        await _notify.PushAsync(ids, "approval", "Task waiting for your approval", $"{who} completed \"{t.Title}\".", "/tasks/approvals");
    }

    // employee row (active) > company row (active) > global row (active) > not required
    public async Task<TaskPolicy> PolicyAsync(int companyId, int userId)
    {
        var rows = await _db.TaskSettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId || s.CompanyId == null).ToListAsync();

        var me = rows.FirstOrDefault(s => s.CompanyId == companyId && s.UserId == userId);
        var co = rows.FirstOrDefault(s => s.CompanyId == companyId && s.UserId == null);
        var gl = rows.FirstOrDefault(s => s.CompanyId == null);
        var pick = me is { IsActive: true } ? me : co is { IsActive: true } ? co : gl is { IsActive: true } ? gl : null;

        return pick == null
            ? new TaskPolicy(false, new LeaveConfig())
            : new TaskPolicy(pick.ApprovalRequired, LeaveFlow.Parse(pick.Config));
    }

    // the task just became "done": finish it, or start the approval chain
    public async Task OnDoneAsync(WorkTask t)
    {
        t.Status = "done";
        t.Progress = 100;
        t.CompletedAt = DateTime.UtcNow;
        t.ApprovalNote = null;

        if (t.Approvals.Count > 0)
        {
            _db.TaskApprovals.RemoveRange(t.Approvals);
            t.Approvals.Clear();
        }

        var pol = await PolicyAsync(t.CompanyId, t.UserId);
        if (!pol.Required) { t.Approval = "none"; return; }

        var rows = await _leave.BuildApproversAsync(t.CompanyId, t.UserId, pol.Config);
        foreach (var a in rows)
            t.Approvals.Add(new TaskApproval { Group = a.Group, Mode = a.Mode, ApproverUserId = a.ApproverUserId, Decision = "pending" });

        t.Approval = rows.Count == 0 ? "approved" : "pending";
    }

    private static bool Satisfied(WorkTask t) =>
        t.Approvals.Count == 0 ||
        (t.Approvals[0].Mode == "all"
            ? t.Approvals.All(a => a.Decision == "approved")
            : t.Approvals.Any(a => a.Decision == "approved"));

    // t must be loaded with Approvals. force = company owner overriding the chain.
    public async Task<string?> DecideAsync(WorkTask t, int uid, bool force, bool approve, string? note)
    {
        if (t.Approval != "pending") return "Nothing to approve.";

        var now = DateTime.UtcNow;
        var mine = t.Approvals.Where(a => a.ApproverUserId == uid && a.Decision == "pending").ToList();
        if (mine.Count == 0 && !force) return "You are not an approver for this task.";

        if (!approve)
        {
            foreach (var a in mine) { a.Decision = "rejected"; a.DecidedAt = now; a.Note = note; }
            foreach (var a in t.Approvals.Where(x => x.Decision == "pending")) a.Decision = "skipped";
            t.Approval = "rejected";
            t.ApprovalNote = note;
            t.Status = "in_progress";
            t.CompletedAt = null;
        }
        else
        {
            if (mine.Count == 0)
            {
                foreach (var a in t.Approvals.Where(x => x.Decision == "pending"))
                {
                    a.Decision = "approved";
                    a.DecidedAt = now;
                    a.Note = "Owner override";
                }
            }
            else
            {
                foreach (var a in mine) { a.Decision = "approved"; a.DecidedAt = now; a.Note = note; }
                if (mine[0].Mode != "all")
                    foreach (var x in t.Approvals.Where(x => x.Decision == "pending")) x.Decision = "skipped";
            }

            if (Satisfied(t)) { t.Approval = "approved"; t.ApprovalNote = note; }
        }

        t.UpdatedAt = now;
        await _db.SaveChangesAsync();

        if (t.Approval is "approved" or "rejected")
            await _notify.PushAsync(new[] { t.UserId }, "info",
                t.Approval == "approved" ? "Task approved" : "Task rejected",
                $"\"{t.Title}\" was {t.Approval}." + (string.IsNullOrWhiteSpace(t.ApprovalNote) ? "" : $"\nNote: {t.ApprovalNote}"),
                t.Kind == "assigned" ? "/tasks/assigned" : "/tasks/daily");

        return null;
    }

    // one shape for every list (employee, manager, owner, main employee)
    public async Task<List<object>> ShapeAsync(IQueryable<WorkTask> q, int viewerUid, int? ownerCompanyId, int take = 1000)
    {
        var list = await q.AsNoTracking()
            .Include(t => t.Files).Include(t => t.Approvals).Include(t => t.User)
            .OrderByDescending(t => t.UpdatedAt).Take(take).ToListAsync();

        var eids = list.Where(t => t.EmployeeId != null).Select(t => t.EmployeeId!.Value).Distinct().ToList();
        var codes = await _db.Employees.AsNoTracking()
            .Where(e => eids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.EmployeeCode);

        var cids = list.Select(t => t.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        var aids = list.SelectMany(t => t.Approvals.Select(a => a.ApproverUserId)).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => aids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        return list.Select(t => (object)new
        {
            t.Id,
            t.CompanyId,
            Company = companies.GetValueOrDefault(t.CompanyId),
            Name = t.User.Name,
            Code = t.EmployeeId != null ? codes.GetValueOrDefault(t.EmployeeId.Value) : "OWNER",
            t.Kind,
            t.Title,
            t.Description,
            t.Status,
            t.Progress,
            t.StartAt,
            t.DueAt,
            t.CreatedAt,
            t.UpdatedAt,
            t.CompletedAt,
            t.Approval,
            t.ApprovalNote,
            Files = t.Files.OrderBy(f => f.Id).Select(f => new { f.Id, f.FileName, f.FilePath, f.Size }).ToList(),
            Approvals = t.Approvals.OrderBy(a => a.Id).Select(a => new
            {
                a.Id,
                a.Group,
                a.Mode,
                a.Decision,
                a.DecidedAt,
                a.Note,
                Approver = names.GetValueOrDefault(a.ApproverUserId)
            }).ToList(),
            CanDecide = t.Approval == "pending" &&
                        (t.Approvals.Any(a => a.ApproverUserId == viewerUid && a.Decision == "pending") ||
                         ownerCompanyId == t.CompanyId)
        }).ToList();
    }

    // what happens when this person completes a task (shown on the Daily tasks page)
    public async Task<object> PreviewAsync(int companyId, int userId)
    {
        var pol = await PolicyAsync(companyId, userId);
        if (!pol.Required) return new { required = false, mode = "any", approvers = new List<object>() };

        var rows = await _leave.BuildApproversAsync(companyId, userId, pol.Config);
        var ids = rows.Select(r => r.ApproverUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        return new
        {
            required = true,
            mode = pol.Config.Mode == "all" ? "all" : "any",
            approvers = rows.Select(r => (object)new { group = r.Group, name = names.GetValueOrDefault(r.ApproverUserId) ?? "—" }).ToList()
        };
    }
}