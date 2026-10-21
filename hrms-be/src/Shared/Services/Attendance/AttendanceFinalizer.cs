using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public class AttendanceFinalizer : BackgroundService
{
    private const int Back = 3;

    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<AttendanceFinalizer> _log;

    public AttendanceFinalizer(IServiceScopeFactory sf, ILogger<AttendanceFinalizer> log)
    {
        _sf = sf;
        _log = log;
    }

    private sealed record Person(int UserId, int? EmployeeId, DateTime Joined);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await RunAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "Attendance finalize failed"); }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = scope.ServiceProvider.GetRequiredService<AttendanceRules>();

        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var from = today.AddDays(-Back);

        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.UserId, c.CreatedAt })
            .ToListAsync(ct);

        foreach (var c in companies)
        {
            var forUser = await rules.BookAsync(c.Id);

            var people = await db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == c.Id && e.IsActive)
                .Select(e => new Person(e.UserId, e.Id, e.CreatedAt))
                .ToListAsync(ct);
            people.Add(new Person(c.UserId, null, c.CreatedAt));

            var have = (await db.Attendances.AsNoTracking()
                    .Where(a => a.CompanyId == c.Id && a.Date >= from && a.Date <= today)
                    .Select(a => new { a.UserId, a.Date })
                    .ToListAsync(ct))
                .Select(a => (a.UserId, a.Date))
                .ToHashSet();

            var add = new List<Attendance>();
            foreach (var p in people)
            {
                var rs = forUser(p.UserId);
                if (!rs.Configured) continue;

                var joined = DateOnly.FromDateTime(Ist.Of(p.Joined));
                for (var d = from; d <= today; d = d.AddDays(1))
                {
                    if (d < joined || have.Contains((p.UserId, d))) continue;
                    if (p.EmployeeId == null && !rs.OwnerOn(d)) continue;

                    var plan = rs.Plan(d);
                    string? status = null, note = null;

                    if (plan.HolidayName != null) { status = "holiday"; note = plan.HolidayName; }
                    else if (plan.Off) { status = "holiday"; note = "Weekly off"; }
                    else if (d < today || rs.PhaseAt(d, nowIst).Name == "closed") status = "absent";

                    if (status == null) continue;

                    add.Add(new Attendance
                    {
                        UserId = p.UserId,
                        EmployeeId = p.EmployeeId,
                        CompanyId = c.Id,
                        Date = d,
                        Status = status,
                        Source = "system",
                        Note = note
                    });
                }
            }

            if (add.Count == 0) continue;

            db.Attendances.AddRange(add);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        }
    }
}