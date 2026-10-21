using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public sealed record DayView(
    string Date, string Status, string? Note, DateTime? CheckInAt, int LateMinutes, string? Source,
    double? Lat, double? Lng, string? PhotoPath);

public sealed record SlotView(string Date, string From, string To, string Status, DateTime? At, double? Lat, double? Lng);

public class AttendanceView
{
    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;

    public AttendanceView(AppDbContext db, AttendanceRules rules)
    {
        _db = db;
        _rules = rules;
    }

    // one person, one row per day (stored rows first, missing days derived)
    public async Task<List<DayView>> DaysAsync(
        int companyId, int userId, bool isOwner, DateTime joinedUtc, DateOnly from, DateOnly to)
    {
        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var rs = await _rules.LoadAsync(companyId, userId);
        var phase = rs.PhaseAt(today, nowIst).Name;
        var joined = DateOnly.FromDateTime(Ist.Of(joinedUtc));

        var att = (await _db.Attendances.AsNoTracking()
                .Where(a => a.UserId == userId && a.Date >= from && a.Date <= to).ToListAsync())
            .ToDictionary(a => a.Date);

        var req = (await _db.AttendanceRequests.AsNoTracking()
                .Where(r => r.UserId == userId && r.Date >= from && r.Date <= to && r.Status != "approved").ToListAsync())
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var list = new List<DayView>();
        for (var d = to; d >= from; d = d.AddDays(-1))
        {
            if (d < joined || (isOwner && !rs.OwnerOn(d))) continue;

            var plan = rs.Plan(d);
            att.TryGetValue(d, out var a);
            req.TryGetValue(d, out var rq);

            string status;
            string? note = null;

            if (a is { Status: "present" }) { status = "present"; note = a.Note; }
            else if (rq?.Status == "pending") status = "pending";
            else if (a != null) { status = a.Status; note = a.Note ?? (rq?.Status == "rejected" ? "Request rejected" : null); }
            else if (!plan.Configured) status = "na";
            else if (plan.HolidayName != null) { status = "holiday"; note = plan.HolidayName; }
            else if (plan.Off) { status = "holiday"; note = "Weekly off"; }
            else if (d == today && phase is "early" or "open" or "request") status = "notyet";
            else { status = "absent"; if (rq?.Status == "rejected") note = "Request rejected"; }

            var p = a is { Status: "present" };
            list.Add(new DayView(d.ToString("yyyy-MM-dd"), status, note,
                p ? a!.CheckInAt : null, p ? a!.LateMinutes : 0, p ? a!.Source : null,
                p ? a!.Lat : null, p ? a!.Lng : null, p ? a!.PhotoPath : null));
        }

        return list;
    }

    // check-in windows of the days the person was present: done / missed / open / upcoming
    public async Task<List<SlotView>> SlotsAsync(int companyId, int userId, DateOnly from, DateOnly to)
    {
        var rs = await _rules.LoadAsync(companyId, userId);
        if (rs.Periods.Count == 0) return new();

        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var nowMin = (int)Math.Floor(nowIst.TimeOfDay.TotalMinutes);

        var present = (await _db.Attendances.AsNoTracking()
                .Where(a => a.UserId == userId && a.Date >= from && a.Date <= to && a.Status == "present")
                .Select(a => a.Date).ToListAsync()).ToHashSet();

        var done = (await _db.AttendanceCheckins.AsNoTracking()
                .Where(c => c.UserId == userId && c.Date >= from && c.Date <= to).ToListAsync())
            .ToDictionary(c => (c.Date, c.SlotFrom));

        var list = new List<SlotView>();
        for (var d = to; d >= from; d = d.AddDays(-1))
        {
            if (!present.Contains(d)) continue;

            foreach (var s in rs.Periods)
            {
                done.TryGetValue((d, s.From), out var c);
                var f = Ist.Mins(s.From);
                var t = Ist.Mins(s.To);
                var status = c != null ? "done" : d < today || nowMin > t ? "missed" : nowMin >= f ? "open" : "upcoming";
                list.Add(new SlotView(d.ToString("yyyy-MM-dd"), s.From, s.To, status, c?.At, c?.Lat, c?.Lng));
            }
        }

        return list;
    }
}