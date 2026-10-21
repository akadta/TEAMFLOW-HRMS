using hrms.Data;
using hrms.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public class NotifHub : Hub { }

// SignalR user id = the "uid" claim of our JWT
public class UidProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("uid")?.Value;
}

// saves a notification row (so it is still there after a refresh / next login) and pushes it live
public class Notifier
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotifHub> _hub;

    public Notifier(AppDbContext db, IHubContext<NotifHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task PushAsync(IEnumerable<int> userIds, string kind, string title, string text, string? link = null)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var rows = ids.Select(u => new Notification { UserId = u, Kind = kind, Title = title, Text = text, Link = link }).ToList();
        _db.Notifications.AddRange(rows);
        await _db.SaveChangesAsync();

        foreach (var r in rows)
            await _hub.Clients.User(r.UserId.ToString()).SendAsync("notify",
                new { id = r.Id, kind = r.Kind, title = r.Title, text = r.Text, link = r.Link, createdAt = r.CreatedAt });
    }
}

public class TaskDispatcher
{
    private readonly AppDbContext _db;
    private readonly Notifier _notify;

    public TaskDispatcher(AppDbContext db, Notifier notify)
    {
        _db = db;
        _notify = notify;
    }

    // a must be loaded with Targets and Files. Creates one "assigned" task per recipient, then alerts them.
    public async Task DispatchAsync(TaskAssignment a)
    {
        var now = DateTime.UtcNow;
        var pairs = new List<(TaskAssignmentTarget T, WorkTask W)>();

        foreach (var t in a.Targets.Where(x => x.TaskId == null))
        {
            var w = new WorkTask
            {
                UserId = t.UserId,
                EmployeeId = t.EmployeeId,
                CompanyId = a.CompanyId,
                Kind = "assigned",
                Title = a.Title,
                Description = a.Description,
                StartAt = now,
                DueAt = a.DueAt,
                Status = "todo"
            };
            foreach (var f in a.Files)
                w.Files.Add(new WorkTaskFile { FileName = f.FileName, FilePath = f.FilePath, Size = f.Size });

            _db.WorkTasks.Add(w);
            pairs.Add((t, w));
        }

        a.Status = "sent";
        a.SentAt = now;
        await _db.SaveChangesAsync();

        foreach (var (t, w) in pairs) t.TaskId = w.Id;
        await _db.SaveChangesAsync();

        var due = a.DueAt == null ? "" : $"\nDue: {Ist.Of(a.DueAt.Value):dd MMM, hh:mm tt}";
        await _notify.PushAsync(a.Targets.Select(x => x.UserId), "task", "New task assigned", a.Title + due, "/tasks/assigned");
    }

    // one shape for the "sent by me" lists and the owner inbox
    public async Task<List<object>> ShapeAsync(List<TaskAssignment> list)
    {
        var tids = list.SelectMany(a => a.Targets).Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).ToList();
        var tasks = await _db.WorkTasks.AsNoTracking().Where(t => tids.Contains(t.Id))
            .Select(t => new { t.Id, t.Status, t.Progress, t.Approval })
            .ToDictionaryAsync(t => t.Id);

        var uids = list.SelectMany(a => a.Targets.Select(t => t.UserId)).Concat(list.Select(a => a.CreatedByUserId)).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => uids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        var cids = list.Select(a => a.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        var eids = list.SelectMany(a => a.Targets).Where(t => t.EmployeeId != null).Select(t => t.EmployeeId!.Value).Distinct().ToList();
        var codes = await _db.Employees.AsNoTracking().Where(e => eids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.EmployeeCode);

        return list.Select(a => (object)new
        {
            a.Id,
            a.CompanyId,
            Company = companies.GetValueOrDefault(a.CompanyId),
            a.Source,
            By = names.GetValueOrDefault(a.CreatedByUserId) ?? "—",
            a.Title,
            a.Description,
            a.DueAt,
            a.SendAt,
            a.Status,
            a.DecisionNote,
            a.CreatedAt,
            a.SentAt,
            Files = a.Files.OrderBy(f => f.Id).Select(f => new { f.FileName, f.FilePath, f.Size }).ToList(),
            Targets = a.Targets.Select(t =>
            {
                var has = t.TaskId != null && tasks.TryGetValue(t.TaskId.Value, out var k);
                var k2 = has ? tasks[t.TaskId!.Value] : null;
                return new
                {
                    t.UserId,
                    Name = names.GetValueOrDefault(t.UserId) ?? "—",
                    Code = t.EmployeeId != null ? codes.GetValueOrDefault(t.EmployeeId.Value) : "OWNER",
                    Status = k2?.Status,
                    Progress = k2?.Progress ?? 0,
                    Approval = k2?.Approval
                };
            }).ToList()
        }).ToList();
    }
}

// every 5 seconds: send the scheduled assignments whose time has come
public class AssignmentScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<AssignmentScheduler> _log;

    public AssignmentScheduler(IServiceScopeFactory sf, ILogger<AssignmentScheduler> log)
    {
        _sf = sf;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sf.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var disp = scope.ServiceProvider.GetRequiredService<TaskDispatcher>();

                var now = DateTime.UtcNow;
                var due = await db.TaskAssignments.Include(a => a.Targets).Include(a => a.Files)
                    .Where(a => a.Status == "scheduled" && a.SendAt != null && a.SendAt <= now)
                    .ToListAsync(ct);

                foreach (var a in due) await disp.DispatchAsync(a);
            }
            catch (Exception ex) { _log.LogError(ex, "Assignment scheduler failed"); }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}