using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hrms.Shared;

namespace hrms.HrmsMainApi.Controllers;

public sealed record MyDayRow(
    string Date, string Status, string? Note, DateTime? CheckInAt, int LateMinutes, string? Source,
    double? Lat, double? Lng, string? PhotoPath);
[ApiController]
[Authorize]
[Route("api/main/my-companies")]
public class MyCompaniesController : BaseController
{
    // null lists = super admin (sees everything)
    private sealed record Scope(List<int>? Companies, List<int>? EmpUsers);

    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;
    private readonly AttendanceView _view;

    public MyCompaniesController(AppDbContext db, AttendanceRules rules, AttendanceView view)
    {
        _db = db;
        _rules = rules;
        _view = view;
    }

    private async Task<Scope> ScopeAsync()
    {
        var super = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == CurrentUserId && u.MainRole != null && u.MainRole.IsActive);
        if (super) return new Scope(null, null);

        // employees this main employee can see (owner-managed companies minus exclusions + direct links)
        var users = (await ReportingScope.VisibleEmployeeUserIdsAsync(_db, CurrentUserId)).ToList();

        // companies: owner is managed, or at least one employee is visible
        var byOwner = await ReportingScope.VisibleCompanyIdsAsync(_db, CurrentUserId);
        var byEmp = await _db.Employees.AsNoTracking()
            .Where(e => users.Contains(e.UserId))
            .Select(e => e.CompanyId)
            .Distinct()
            .ToListAsync();

        return new Scope(byOwner.Union(byEmp).ToList(), users);
    }

    private static bool SeesCompany(Scope s, int companyId) => s.Companies == null || s.Companies.Contains(companyId);
    private static bool SeesUser(Scope s, int userId) => s.EmpUsers == null || s.EmpUsers.Contains(userId);

    private IActionResult NoAccess() =>
        StatusCode(403, new { message = "You do not have access to this company." });

    // company + employee access in one go. null = allowed
    private async Task<IActionResult?> GuardEmployee(Scope sc, int companyId, int employeeId)
    {
        if (!SeesCompany(sc, companyId)) return NoAccess();

        var uid = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId && e.CompanyId == companyId)
            .Select(e => (int?)e.UserId)
            .FirstOrDefaultAsync();
        if (uid == null) return NotFound(new { message = "Employee not found." });

        return SeesUser(sc, uid.Value)
            ? null
            : StatusCode(403, new { message = "You do not have access to this employee." });
    }

    // GET api/main/my-companies?search=
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search)
    {
        var sc = await ScopeAsync();

        var q = _db.Companies.AsNoTracking().AsQueryable();
        if (sc.Companies != null)
        {
            var cids = sc.Companies;
            q = q.Where(c => cids.Contains(c.Id));
        }

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0) q = q.Where(c => c.Name.ToLower().Contains(s) || c.OwnerName.ToLower().Contains(s));

        var all = sc.EmpUsers == null;
        var users = sc.EmpUsers ?? new List<int>();

        var items = await q.OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.OwnerName,
                c.Logo,
                c.IsActive,
                Created = c.CreatedAt,
                // only the employees this person is allowed to see
                Employees = _db.Employees.Count(e => e.CompanyId == c.Id && (all || users.Contains(e.UserId)))
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET api/main/my-companies/5/employees
    [HttpGet("{companyId:int}/employees")]
    public async Task<IActionResult> Employees(int companyId)
    {
        var sc = await ScopeAsync();
        if (!SeesCompany(sc, companyId)) return NoAccess();

        var company = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Id, c.Name, c.OwnerName, c.Logo })
            .FirstOrDefaultAsync();
        if (company == null) return NotFound(new { message = "Company not found." });

        var eq = _db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId);
        if (sc.EmpUsers != null)
        {
            var users = sc.EmpUsers;
            eq = eq.Where(e => users.Contains(e.UserId));
        }

        var items = await eq
            .OrderBy(e => e.User.Name)
            .Take(1000)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.EmployeeCode,
                Name = e.User.Name,
                Email = e.User.Email,
                Phone = e.User.PhoneNumber,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.WorkType,
                e.ProfilePicture,
                e.IsActive,
                Joined = e.CreatedAt
            })
            .ToListAsync();

        return Ok(new { company, items });
    }

    // GET api/main/my-companies/5/employees/12   (full profile)
    [HttpGet("{companyId:int}/employees/{employeeId:int}")]
    public async Task<IActionResult> EmployeeDetail(int companyId, int employeeId)
    {
        var sc = await ScopeAsync();
        var deny = await GuardEmployee(sc, companyId, employeeId);
        if (deny != null) return deny;

        var e = await _db.Employees.AsNoTracking()
            .Where(x => x.Id == employeeId && x.CompanyId == companyId)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.EmployeeCode,
                Name = x.User.Name,
                Email = x.User.Email,
                Phone = x.User.PhoneNumber,
                x.OfficialEmail,
                x.EmergencyNumber,
                x.DateOfBirth,
                x.Gender,
                x.BloodGroup,
                x.MaritalStatus,
                x.Nationality,
                x.AadharNumber,
                x.PanNumber,
                x.Address,
                x.ProfilePicture,
                x.HighestQualification,
                x.Institution,
                x.YearOfPassing,
                x.WorkType,
                Role = x.CompanyRole != null ? x.CompanyRole.Name : null,
                x.IsActive,
                Joined = x.CreatedAt,
                Company = x.Company.Name,
                Documents = x.Documents.OrderBy(d => d.Id)
                    .Select(d => new { d.Type, d.Title, d.Description, d.FilePath }).ToList(),
                Histories = x.Histories.OrderByDescending(h => h.StartDate)
                    .Select(h => new { h.Employer, h.StartDate, h.EndDate, h.ReasonForLeaving, h.RelievingLetter }).ToList()
            })
            .FirstAsync();

        var mids = await _db.ReportingManagers.AsNoTracking()
            .Where(r => r.UserId == e.UserId).Select(r => r.ManagerUserId).ToListAsync();
        var managers = await _db.Users.AsNoTracking()
            .Where(u => mids.Contains(u.Id)).OrderBy(u => u.Name).Select(u => u.Name).ToListAsync();
        var owner = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId).Select(c => c.OwnerName).FirstAsync();

        return Ok(new { employee = e, managers, owner });
    }

    // GET api/main/my-companies/5/employees/12/attendance?from=2026-10-01&to=2026-10-31   (max 366 days)
    [HttpGet("{companyId:int}/employees/{employeeId:int}/attendance")]
    public async Task<IActionResult> Attendance(
        int companyId, int employeeId,
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        var sc = await ScopeAsync();
        var deny = await GuardEmployee(sc, companyId, employeeId);
        if (deny != null) return deny;

        var emp = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId && e.CompanyId == companyId)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.EmployeeCode,
                Name = e.User.Name,
                Email = e.User.Email,
                Phone = e.User.PhoneNumber,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.WorkType,
                e.ProfilePicture,
                e.IsActive,
                Company = e.Company.Name,
                Joined = e.CreatedAt
            })
            .FirstAsync();

        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var to = dateTo == null || dateTo > today ? today : dateTo.Value;
        var from = dateFrom ?? new DateOnly(to.Year, to.Month, 1);

        if (from > to) return BadRequest(new { message = "From date is after To date." });
        if (to.DayNumber - from.DayNumber + 1 > 366) return BadRequest(new { message = "Max 366 days at a time." });

        var rs = await _rules.LoadAsync(companyId);
        var todayPhase = rs.PhaseAt(today, nowIst).Name;
        var joined = DateOnly.FromDateTime(Ist.Of(emp.Joined));

        var att = (await _db.Attendances.AsNoTracking()
                .Where(a => a.UserId == emp.UserId && a.Date >= from && a.Date <= to)
                .ToListAsync())
            .ToDictionary(a => a.Date);

        var reqList = await _db.AttendanceRequests.AsNoTracking()
            .Where(r => r.UserId == emp.UserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Date,
                r.Reason,
                r.Status,
                r.LateMinutes,
                r.RequestedAt,
                r.DecidedAt,
                r.DecisionNote,
                r.DecidedByUserId,
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList()
            })
            .ToListAsync();

        var latestOpen = reqList.Where(r => r.Status != "approved")
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var deciderIds = reqList.Where(r => r.DecidedByUserId != null).Select(r => r.DecidedByUserId!.Value).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => deciderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var rows = new List<MyDayRow>();
        for (var d = to; d >= from; d = d.AddDays(-1))
        {
            if (d < joined) continue;

            var plan = rs.Plan(d);
            att.TryGetValue(d, out var a);
            latestOpen.TryGetValue(d, out var rq);

            string status;
            string? note = null;

            if (a is { Status: "present" }) { status = "present"; note = a.Note; }
            else if (rq?.Status == "pending") status = "pending";
            else if (a != null) { status = a.Status; note = a.Note ?? (rq?.Status == "rejected" ? "Request rejected" : null); }
            else if (!plan.Configured) status = "na";
            else if (plan.HolidayName != null) { status = "holiday"; note = plan.HolidayName; }
            else if (plan.Off) { status = "holiday"; note = "Weekly off"; }
            else if (d == today && todayPhase is "early" or "open" or "request") status = "notyet";
            else { status = "absent"; if (rq?.Status == "rejected") note = "Request rejected"; }

            var present = a is { Status: "present" };
            rows.Add(new MyDayRow(d.ToString("yyyy-MM-dd"), status, note,
                present ? a!.CheckInAt : null, present ? a!.LateMinutes : 0, present ? a!.Source : null,
                present ? a!.Lat : null, present ? a!.Lng : null, present ? a!.PhotoPath : null));
        }

        return Ok(new
        {
            company = new { id = companyId, name = emp.Company },
            employee = new
            {
                emp.Id,
                emp.UserId,
                emp.EmployeeCode,
                emp.Name,
                emp.Email,
                emp.Phone,
                emp.Role,
                emp.WorkType,
                emp.ProfilePicture,
                emp.IsActive
            },
            dateFrom = from,
            dateTo = to,
            rows,
            requests = reqList.Select(r => new
            {
                r.Id,
                Date = r.Date.ToString("yyyy-MM-dd"),
                r.Reason,
                r.Status,
                r.LateMinutes,
                r.RequestedAt,
                r.DecidedAt,
                r.DecisionNote,
                DecidedBy = r.DecidedByUserId != null && names.TryGetValue(r.DecidedByUserId.Value, out var n) ? n : null,
                r.Files
            })
        });
    }

    private static (DateOnly From, DateOnly To)? Span(DateOnly? f, DateOnly? t)
    {
        var today = Ist.Today;
        var to = t == null || t > today ? today : t.Value;
        var from = f ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to || to.DayNumber - from.DayNumber + 1 > 366) return null;
        return (from, to);
    }

    // GET api/main/my-companies/5/employees/12/checkins?from=&to=
    [HttpGet("{companyId:int}/employees/{employeeId:int}/checkins")]
    public async Task<IActionResult> Checkins(
        int companyId, int employeeId,
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        var sc = await ScopeAsync();
        var deny = await GuardEmployee(sc, companyId, employeeId);
        if (deny != null) return deny;

        var span = Span(dateFrom, dateTo);
        if (span == null) return BadRequest(new { message = "Choose a valid date range (max 366 days)." });
        var (from, to) = span.Value;

        var uid = await _db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => e.UserId).FirstAsync();
        return Ok(new { rows = await _view.SlotsAsync(companyId, uid, from, to) });
    }

    // GET api/main/my-companies/5/employees/12/logins?from=&to=   (every captured login location)
    [HttpGet("{companyId:int}/employees/{employeeId:int}/logins")]
    public async Task<IActionResult> Logins(
        int companyId, int employeeId,
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        var sc = await ScopeAsync();
        var deny = await GuardEmployee(sc, companyId, employeeId);
        if (deny != null) return deny;

        var span = Span(dateFrom, dateTo);
        if (span == null) return BadRequest(new { message = "Choose a valid date range (max 366 days)." });
        var (from, to) = span.Value;

        var uid = await _db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => e.UserId).FirstAsync();
        var fromUtc = Ist.ToUtc(from, 0);
        var toUtc = Ist.ToUtc(to.AddDays(1), 0);

        var rows = await _db.LoginLocations.AsNoTracking()
            .Where(l => l.UserId == uid && l.At >= fromUtc && l.At < toUtc)
            .OrderByDescending(l => l.At).Take(2000)
            .Select(l => new { l.At, l.Lat, l.Lng, l.Accuracy })
            .ToListAsync();

        return Ok(new { rows });
    }
}