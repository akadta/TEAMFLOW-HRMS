using System.Text.Json;
using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public sealed record StoredRule(int Day, bool Off, string? From, string? To, string? Grace);
public sealed record DayPlan(bool Configured, bool Off, string From, string To, string? Grace, string? HolidayName, string? HolidayColor);
public sealed record Phase(string Name, DateTime? NextChangeUtc);
public sealed record PeriodSlot(string From, string To);

public sealed class RuleSet
{
    public bool Configured { get; init; }
    public bool OwnerAttends { get; init; }
    public HashSet<int> OwnerDays { get; init; } = new();
    public bool OwnerOn(DateOnly d) => OwnerAttends && OwnerDays.Contains((int)d.DayOfWeek);
    public string From { get; init; } = "09:00";
    public string To { get; init; } = "18:00";
    public string? Grace { get; init; }
    public bool RequireLocation { get; init; }
    public bool RequirePhoto { get; init; }
    public bool LoginLocation { get; init; }
    public List<PeriodSlot> Periods { get; init; } = new();
    public List<StoredRule> Days { get; init; } = new();
    public List<Holiday> Holidays { get; init; } = new();

    public DayPlan Plan(DateOnly d)
    {
        if (!Configured) return new(false, false, From, To, null, null, null);

        var hol = Holidays.FirstOrDefault(h => Hit(h, d));
        var r = Days.FirstOrDefault(x => x.Day == (int)d.DayOfWeek);
        var custom = r is { Off: false, From: not null, To: not null };

        return new(true, r?.Off == true,
            custom ? r!.From! : From,
            custom ? r!.To! : To,
            custom ? r!.Grace : Grace,
            hol?.Name, hol?.Color);
    }

    public Phase PhaseAt(DateOnly date, DateTime nowIst)
    {
        var p = Plan(date);
        if (!p.Configured) return new("unconfigured", null);
        if (p.HolidayName != null) return new("holiday", null);
        if (p.Off) return new("off", null);

        var now = nowIst.TimeOfDay.TotalMinutes;
        var nowMin = (int)Math.Floor(now);
        var from = Ist.Mins(p.From);
        var to = Ist.Mins(p.To);

        if (now < from) return new("early", Ist.ToUtc(date, from));

        if (p.Grace != null)
        {
            var g = Ist.Mins(p.Grace);
            if (nowMin <= g) return new("open", Ist.ToUtc(date, g + 1));
            if (nowMin < to) return new("request", Ist.ToUtc(date, to));
            return new("closed", null);
        }

        return nowMin < to ? new("open", Ist.ToUtc(date, to)) : new("closed", null);
    }

    public static int Late(DayPlan p, DateOnly d, DateTime nowIst)
    {
        var start = d.ToDateTime(TimeOnly.MinValue).AddMinutes(Ist.Mins(p.From));
        return Math.Max(0, (int)Math.Floor((nowIst - start).TotalMinutes));
    }

    private static bool Hit(Holiday h, DateOnly d) => h.Kind switch
    {
        "weekly" => h.Weekday == (int)d.DayOfWeek,
        "nth" => h.Weekday == (int)d.DayOfWeek
                 && (h.Nth ?? string.Empty).Split(',').Contains(((d.Day - 1) / 7 + 1).ToString()),
        _ => h.Date is { } hd && hd.Month == d.Month && hd.Day == d.Day && (h.Yearly || hd.Year == d.Year)
    };
}

public class AttendanceRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public AttendanceRules(AppDbContext db) => _db = db;

    // timing + policy: employee row (active) > company row (active) > global row (active)
    // holidays: company row active > global row active (employee rows never affect holidays)
    public async Task<RuleSet> LoadAsync(int companyId, int? userId = null)
    {
        var rows = await _db.AttendanceSettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId || s.CompanyId == null)
            .ToListAsync();

        var co = rows.FirstOrDefault(s => s.CompanyId == companyId && s.UserId == null);
        var gl = rows.FirstOrDefault(s => s.CompanyId == null);
        var me = userId == null ? null : rows.FirstOrDefault(s => s.CompanyId == companyId && s.UserId == userId);

        var timing = me is { TimingActive: true } ? me
            : co is { TimingActive: true } ? co
            : gl is { TimingActive: true } ? gl
            : null;

        int? hid = null;
        var useHol = false;
        if (co is { HolidaysActive: true }) { hid = companyId; useHol = true; }
        else if (gl is { HolidaysActive: true }) { useHol = true; }

        var holidays = useHol
            ? await _db.Holidays.AsNoTracking().Where(h => h.IsActive && h.CompanyId == hid).ToListAsync()
            : new List<Holiday>();

        return new RuleSet
        {
            Configured = timing != null,
            OwnerAttends = timing?.OwnerAttends ?? false,
            OwnerDays = (timing?.OwnerDays ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => int.TryParse(x, out var n) ? n : -1)
    .Where(n => n is >= 0 and <= 6)
    .ToHashSet(),
            From = timing?.TimeFrom ?? "09:00",
            To = timing?.TimeTo ?? "18:00",
            Grace = timing?.GraceUntil,
            RequireLocation = timing?.RequireLocation ?? false,
            RequirePhoto = timing?.RequirePhoto ?? false,
            LoginLocation = timing?.LoginLocation ?? false,
            Periods = timing == null ? new() : ParseList<PeriodSlot>(timing.Periods),
            Days = timing == null ? new() : ParseList<StoredRule>(timing.DayRules),
            Holidays = holidays,
        };
    }

    // company-level rules + a personal rule set only for people who have their own active setting
    public async Task<Func<int, RuleSet>> BookAsync(int companyId)
    {
        var company = await LoadAsync(companyId);
        var users = await _db.AttendanceSettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.UserId != null && s.TimingActive)
            .Select(s => s.UserId!.Value)
            .ToListAsync();

        var own = new Dictionary<int, RuleSet>();
        foreach (var u in users) own[u] = await LoadAsync(companyId, u);

        return uid => own.TryGetValue(uid, out var r) ? r : company;
    }
    private static List<T> ParseList<T>(string? json) =>
    string.IsNullOrWhiteSpace(json)
        ? new()
        : JsonSerializer.Deserialize<List<T>>(json, Json) ?? new();
}