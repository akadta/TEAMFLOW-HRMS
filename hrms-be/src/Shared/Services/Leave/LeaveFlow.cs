using System.Text.Json;
using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public sealed class LeaveConfig
{
    public bool Managers { get; set; } = true;
    public bool Owner { get; set; }
    public bool Main { get; set; }
    public string Mode { get; set; } = "any";
}

public sealed record LeaveEffective(bool Active, int? MaxDays, LeaveConfig Config);
public record LeaveDecideInput(bool Approve, string? Note);

public class LeaveFlow
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly Notifier _notify;

    public LeaveFlow(AppDbContext db, Notifier notify)
    {
        _db = db;
        _notify = notify;
    }

    public async Task NotifyApproversAsync(LeaveRequest r)
    {
        var ids = r.Approvals.Where(a => a.Decision == "pending").Select(a => a.ApproverUserId).Distinct().ToList();
        if (ids.Count == 0) return;

        var who = await _db.Users.AsNoTracking().Where(u => u.Id == r.UserId).Select(u => u.Name).FirstAsync();
        await _notify.PushAsync(ids, "approval", "Leave request to approve",
            $"{who} asked for {r.Days.Count} day(s) of leave.", "/leave/approvals");
    }

    private Task TellAsync(LeaveRequest r) =>
        _notify.PushAsync(new[] { r.UserId }, "info", r.Status == "approved" ? "Leave approved" : "Leave rejected",
            $"Your leave request ({r.Days.Count} day(s)) was {r.Status}." +
            (string.IsNullOrWhiteSpace(r.DecisionNote) ? "" : $"\nNote: {r.DecisionNote}"), "/my-requests");

    public static LeaveConfig Parse(string? json)
    {
        try { return JsonSerializer.Deserialize<LeaveConfig>(string.IsNullOrWhiteSpace(json) ? "{}" : json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public static string Serialize(LeaveConfig c) => JsonSerializer.Serialize(c, Json);

    public async Task<LeaveEffective> EffectiveAsync(int companyId)
    {
        var rows = await _db.LeaveSettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId || s.CompanyId == null).ToListAsync();

        var co = rows.FirstOrDefault(s => s.CompanyId == companyId);
        var gl = rows.FirstOrDefault(s => s.CompanyId == null);
        var pick = co is { IsActive: true } ? co : gl is { IsActive: true } ? gl : null;

        return pick == null
            ? new LeaveEffective(false, null, new LeaveConfig())
            : new LeaveEffective(true, pick.MaxDays, Parse(pick.Config));
    }

    public async Task<List<LeaveApproval>> BuildApproversAsync(int companyId, int userId, LeaveConfig cfg)
    {
        var ownerId = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId).Select(c => c.UserId).FirstAsync();
        var mode = cfg.Mode == "all" ? "all" : "any";
        var rows = new List<LeaveApproval>();

        async Task AddAsync(string group, IEnumerable<int> ids)
        {
            var list = ids.Where(i => i != userId).Distinct().ToList();
            var active = await _db.Users.AsNoTracking()
                .Where(u => list.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var id in active)
                rows.Add(new LeaveApproval { Group = group, Mode = mode, ApproverUserId = id, Decision = "pending" });
        }

        if (cfg.Managers)
            await AddAsync("managers", await _db.ReportingManagers.AsNoTracking()
                .Where(r => r.UserId == userId).Select(r => r.ManagerUserId).ToListAsync());

        if (cfg.Owner) await AddAsync("owner", new[] { ownerId });

        if (cfg.Main)
        {
            var mains = await _db.ReportingManagers.AsNoTracking()
                .Where(r => r.UserId == ownerId &&
                            _db.MainEmployees.Any(m => m.UserId == r.ManagerUserId && m.IsActive))
                .Select(r => r.ManagerUserId).ToListAsync();
            var blocked = await _db.ReportingExclusions.AsNoTracking()
                .Where(x => x.UserId == userId).Select(x => x.ManagerUserId).ToListAsync();
            await AddAsync("main", mains.Where(m => !blocked.Contains(m)));
        }

        if (rows.Count == 0 && userId != ownerId) await AddAsync("owner", new[] { ownerId });
        return rows;
    }

    private static bool Satisfied(LeaveRequest r) =>
        r.Approvals.Count == 0 ||
        (r.Approvals[0].Mode == "all"
            ? r.Approvals.All(a => a.Decision == "approved")
            : r.Approvals.Any(a => a.Decision == "approved"));

    private static void Close(LeaveRequest r, string status, int uid, string? note, DateTime now)
    {
        r.Status = status;
        r.DecidedAt = now;
        r.DecidedByUserId = uid;
        r.DecisionNote = note;
    }

    public async Task<string?> DecideAsync(LeaveRequest r, int uid, bool force, bool approve, string? note)
    {
        if (r.Status != "pending") return "Already decided.";

        var now = DateTime.UtcNow;
        var mine = r.Approvals.Where(a => a.ApproverUserId == uid && a.Decision == "pending").ToList();
        if (mine.Count == 0 && !force) return "You are not an approver for this request.";

        if (!approve)
        {
            foreach (var a in mine) { a.Decision = "rejected"; a.DecidedAt = now; a.Note = note; }
            foreach (var a in r.Approvals.Where(x => x.Decision == "pending")) a.Decision = "skipped";
            Close(r, "rejected", uid, note, now);
            await _db.SaveChangesAsync();
            await TellAsync(r);
            await _db.SaveChangesAsync();
            return null;
        }

        if (mine.Count == 0)
        {
            foreach (var a in r.Approvals.Where(x => x.Decision == "pending"))
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
                foreach (var x in r.Approvals.Where(x => x.Decision == "pending")) x.Decision = "skipped";
        }

        if (Satisfied(r))
        {
            Close(r, "approved", uid, note, now);
            await WriteLeaveRowsAsync(r);
            await _db.SaveChangesAsync();
            await TellAsync(r);
        }

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task WriteLeaveRowsAsync(LeaveRequest r)
    {
        var note = "Leave: " + (r.Reason.Length > 120 ? r.Reason[..120] : r.Reason);

        foreach (var d in r.Days)
        {
            var row = await _db.Attendances.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.Date == d.Date);
            if (row is { Status: "present" }) continue;

            if (row == null)
            {
                row = new Attendance { UserId = r.UserId, EmployeeId = r.EmployeeId, CompanyId = r.CompanyId, Date = d.Date };
                _db.Attendances.Add(row);
            }

            row.Status = "leave";
            row.Source = "leave";
            row.CheckInAt = null;
            row.LateMinutes = 0;
            row.RequestId = null;
            row.Note = note;
        }
    }

    public async Task<List<object>> ListAsync(int uid, int? ownerCompanyId, bool pendingOnly)
    {
        var mineIds = _db.LeaveApprovals.Where(a => a.ApproverUserId == uid).Select(a => a.RequestId);

        var q = _db.LeaveRequests.AsNoTracking()
            .Where(r => mineIds.Contains(r.Id) || (ownerCompanyId != null && r.CompanyId == ownerCompanyId));
        if (pendingOnly) q = q.Where(r => r.Status == "pending");

        var raw = await q.OrderByDescending(r => r.Id).Take(200)
            .Select(r => new
            {
                r.Id,
                r.CompanyId,
                Company = _db.Companies.Where(c => c.Id == r.CompanyId).Select(c => c.Name).FirstOrDefault(),
                Name = r.User.Name,
                Code = r.Employee != null ? r.Employee.EmployeeCode : "OWNER",
                r.Reason,
                r.Status,
                r.CreatedAt,
                r.DecidedAt,
                r.DecisionNote,
                r.DecidedByUserId,
                Days = r.Days.OrderBy(d => d.Date).Select(d => d.Date).ToList(),
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList(),
                Approvals = r.Approvals.OrderBy(a => a.Id)
                    .Select(a => new { a.Id, a.Group, a.Mode, a.ApproverUserId, a.Decision, a.DecidedAt, a.Note })
                    .ToList()
            })
            .ToListAsync();

        var ids = raw.SelectMany(x => x.Approvals.Select(a => a.ApproverUserId))
            .Concat(raw.Where(x => x.DecidedByUserId != null).Select(x => x.DecidedByUserId!.Value))
            .Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        return raw.Select(x => (object)new
        {
            x.Id,
            x.CompanyId,
            x.Company,
            x.Name,
            x.Code,
            x.Reason,
            x.Status,
            x.CreatedAt,
            x.DecidedAt,
            x.DecisionNote,
            DecidedBy = x.DecidedByUserId == null ? null : names.GetValueOrDefault(x.DecidedByUserId.Value),
            x.Days,
            x.Files,
            Approvals = x.Approvals.Select(a => new
            {
                a.Id,
                a.Group,
                a.Mode,
                a.Decision,
                a.DecidedAt,
                a.Note,
                Approver = names.GetValueOrDefault(a.ApproverUserId)
            }).ToList(),
            CanDecide = x.Status == "pending" &&
                        (x.Approvals.Any(a => a.ApproverUserId == uid && a.Decision == "pending") ||
                         ownerCompanyId == x.CompanyId)
        }).ToList();
    }

    // read-only list of leave requests for any filter (same shape as ListAsync, nobody can decide here)
    public async Task<List<object>> ReportAsync(IQueryable<LeaveRequest> q)
    {
        var raw = await q.OrderByDescending(r => r.Id).Take(1000)
            .Select(r => new
            {
                r.Id,
                r.CompanyId,
                Company = _db.Companies.Where(c => c.Id == r.CompanyId).Select(c => c.Name).FirstOrDefault(),
                Name = r.User.Name,
                Code = r.Employee != null ? r.Employee.EmployeeCode : "OWNER",
                r.Reason,
                r.Status,
                r.CreatedAt,
                r.DecidedAt,
                r.DecisionNote,
                r.DecidedByUserId,
                Days = r.Days.OrderBy(d => d.Date).Select(d => d.Date).ToList(),
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList(),
                Approvals = r.Approvals.OrderBy(a => a.Id)
                    .Select(a => new { a.Id, a.Group, a.Mode, a.ApproverUserId, a.Decision, a.DecidedAt, a.Note }).ToList()
            })
            .ToListAsync();

        var ids = raw.SelectMany(x => x.Approvals.Select(a => a.ApproverUserId))
            .Concat(raw.Where(x => x.DecidedByUserId != null).Select(x => x.DecidedByUserId!.Value))
            .Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        return raw.Select(x => (object)new
        {
            x.Id,
            x.CompanyId,
            x.Company,
            x.Name,
            x.Code,
            x.Reason,
            x.Status,
            x.CreatedAt,
            x.DecidedAt,
            x.DecisionNote,
            DecidedBy = x.DecidedByUserId == null ? null : names.GetValueOrDefault(x.DecidedByUserId.Value),
            x.Days,
            x.Files,
            Approvals = x.Approvals.Select(a => new
            {
                a.Id,
                a.Group,
                a.Mode,
                a.Decision,
                a.DecidedAt,
                a.Note,
                Approver = names.GetValueOrDefault(a.ApproverUserId)
            }).ToList(),
            CanDecide = false
        }).ToList();
    }
}