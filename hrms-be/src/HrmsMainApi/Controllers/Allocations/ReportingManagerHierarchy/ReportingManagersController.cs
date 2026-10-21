using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record ReportingUserInfo(
    int UserId, int? EmployeeId, string Name, string Email, string? Code, string? Role,
    string? ProfilePicture, int CompanyId, string CompanyName, bool IsOwner, bool IsActive, bool IsMain);

public record ReportingPersonDto(
    int UserId, int? EmployeeId, string Name, string Email, string? Code, string? Role,
    string? ProfilePicture, int CompanyId, string CompanyName, bool IsOwner, bool IsActive, bool IsMain,
    List<ReportingUserInfo> Managers, ReportingUserInfo? DefaultManager, int ReportsCount,
    List<ReportingUserInfo> InheritedMain, List<ReportingUserInfo> BlockedMain);
public record AddReportingManagerRequest(int ManagerUserId);

[ApiController]
[Authorize]
[Route("api/main/reporting-managers")]
public class ReportingManagersController : BaseController
{
    private const string MainName = "Main team";   // companyId 0 = the main team

    private readonly AppDbContext _db;
    public ReportingManagersController(AppDbContext db) => _db = db;

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // ---------- helpers ----------

    // company employee -> their company; main employee -> "Main team" (id 0); company owner -> the company they own
    private async Task<Dictionary<int, ReportingUserInfo>> LoadInfo(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var map = new Dictionary<int, ReportingUserInfo>();
        if (ids.Count == 0) return map;

        foreach (var x in await _db.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.UserId))
            .Select(e => new ReportingUserInfo(
                e.UserId, e.Id, e.User.Name, e.User.Email, e.EmployeeCode,
                e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.ProfilePicture, e.CompanyId, e.Company.Name, false, e.IsActive, false))
            .ToListAsync())
            map[x.UserId] = x;

        var rest = ids.Where(i => !map.ContainsKey(i)).ToList();
        if (rest.Count > 0)
        {
            foreach (var x in await _db.MainEmployees.AsNoTracking()
                .Where(m => rest.Contains(m.UserId))
                .Select(m => new ReportingUserInfo(
                    m.UserId, m.Id, m.User.Name, m.User.Email, m.EmployeeCode,
                    m.Role.Name, m.ProfilePicture, 0, MainName, false, m.IsActive, true))
                .ToListAsync())
                map[x.UserId] = x;

            rest = ids.Where(i => !map.ContainsKey(i)).ToList();
        }

        if (rest.Count > 0)
        {
            foreach (var x in await _db.Companies.AsNoTracking()
                .Where(c => rest.Contains(c.UserId))
                .Select(c => new ReportingUserInfo(
                    c.UserId, null, c.User.Name, c.User.Email, null, "Owner",
                    c.Logo, c.Id, c.Name, true, c.IsActive, false))
                .ToListAsync())
                map[x.UserId] = x;
        }

        return map;
    }

    private async Task<List<ReportingPersonDto>> BuildPeople(List<int> userIds)
    {
        userIds = userIds.Distinct().ToList();
        if (userIds.Count == 0) return new();

        var links = await _db.ReportingManagers.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId))
            .Select(r => new { r.UserId, r.ManagerUserId })
            .ToListAsync();

        var explicitReports = await _db.ReportingManagers.AsNoTracking()
            .Where(r => userIds.Contains(r.ManagerUserId))
            .GroupBy(r => r.ManagerUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var info = await LoadInfo(userIds.Concat(links.Select(l => l.ManagerUserId)));

        // default manager = owner of the person's CURRENT company (computed, never stored; main team has none)
        var companyIds = userIds.Where(info.ContainsKey).Select(id => info[id].CompanyId).Where(c => c != 0).Distinct().ToList();
        var ownerOf = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.UserId);

        var missing = ownerOf.Values.Where(id => !info.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            foreach (var kv in await LoadInfo(missing)) info[kv.Key] = kv.Value;

        // main team managers of the owners: every employee of those companies inherits them automatically
        var ownerIds = ownerOf.Values.ToList();
        var ownerMain = await _db.ReportingManagers.AsNoTracking()
            .Where(r => ownerIds.Contains(r.UserId) &&
                        _db.MainEmployees.Any(m => m.UserId == r.ManagerUserId && m.IsActive))
            .Select(r => new { r.UserId, r.ManagerUserId })
            .ToListAsync();

        var excluded = await _db.ReportingExclusions.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.ManagerUserId })
            .ToListAsync();

        var needInfo = ownerMain.Select(x => x.ManagerUserId).Where(id => !info.ContainsKey(id)).Distinct().ToList();
        if (needInfo.Count > 0)
            foreach (var kv in await LoadInfo(needInfo)) info[kv.Key] = kv.Value;

        var result = new List<ReportingPersonDto>();
        foreach (var id in userIds)
        {
            if (!info.TryGetValue(id, out var u)) continue;

            var managers = links.Where(l => l.UserId == id)
                .Select(l => l.ManagerUserId).Distinct()
                .Where(info.ContainsKey)
                .Select(m => info[m])
                .OrderBy(m => m.Name)
                .ToList();

            ReportingUserInfo? def = null;
            if (!u.IsOwner && !u.IsMain
                && ownerOf.TryGetValue(u.CompanyId, out var ownerId)
                && managers.All(m => m.UserId != ownerId)
                && info.TryGetValue(ownerId, out var oi))
                def = oi;

            // inherited main managers (from the owner), minus the ones excluded for this person
            var inherited = new List<ReportingUserInfo>();
            var blocked = new List<ReportingUserInfo>();
            if (!u.IsOwner && !u.IsMain && ownerOf.TryGetValue(u.CompanyId, out var oid))
            {
                foreach (var mm in ownerMain.Where(x => x.UserId == oid).Select(x => x.ManagerUserId).Distinct())
                {
                    if (!info.TryGetValue(mm, out var mi) || managers.Any(m => m.UserId == mm)) continue;

                    if (excluded.Any(x => x.UserId == id && x.ManagerUserId == mm)) blocked.Add(mi);
                    else inherited.Add(mi);
                }
            }

            var reports = explicitReports.GetValueOrDefault(id);
            if (u.IsOwner)
            {
                // every active employee of this company reports to the owner automatically
                reports += await _db.Employees.AsNoTracking().CountAsync(e =>
                    e.CompanyId == u.CompanyId && e.IsActive &&
                    !_db.ReportingManagers.Any(r => r.UserId == e.UserId && r.ManagerUserId == id));
            }
            else if (u.IsMain)
            {
                // every employee of the companies whose owner reports to this person, minus exclusions
                reports += await _db.Employees.AsNoTracking().CountAsync(e =>
                    e.IsActive &&
                    _db.ReportingManagers.Any(r => r.ManagerUserId == id && r.UserId == e.Company.UserId) &&
                    !_db.ReportingExclusions.Any(x => x.UserId == e.UserId && x.ManagerUserId == id) &&
                    !_db.ReportingManagers.Any(r => r.UserId == e.UserId && r.ManagerUserId == id));
            }

            result.Add(new ReportingPersonDto(
                u.UserId, u.EmployeeId, u.Name, u.Email, u.Code, u.Role, u.ProfilePicture,
                u.CompanyId, u.CompanyName, u.IsOwner, u.IsActive, u.IsMain,
                managers, def, reports, inherited, blocked));
        }

        return result;
    }

    private static IQueryable<Employee> ApplySearch(IQueryable<Employee> q, string s)
    {
        if (s.Length == 0) return q;
        return q.Where(e =>
            e.User.Name.ToLower().Contains(s) ||
            e.User.Email.ToLower().Contains(s) ||
            e.EmployeeCode.ToLower().Contains(s) ||
            (e.CompanyRole != null && e.CompanyRole.Name.ToLower().Contains(s)));
    }

    private static IQueryable<MainEmployee> ApplyMainSearch(IQueryable<MainEmployee> q, string s)
    {
        if (s.Length == 0) return q;
        return q.Where(m =>
            m.User.Name.ToLower().Contains(s) ||
            m.User.Email.ToLower().Contains(s) ||
            m.EmployeeCode.ToLower().Contains(s) ||
            m.Role.Name.ToLower().Contains(s));
    }

    // ---------- reads ----------

    // GET api/main/reporting-managers/overview?companyId=1&search=&page=1&pageSize=50   (companyId 0 = main team)
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] int companyId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var s = (search ?? string.Empty).Trim().ToLower();

        if (companyId == 0)
        {
            var mq = ApplyMainSearch(_db.MainEmployees.AsNoTracking(), s);
            var mtotal = await mq.CountAsync();
            var mids = await mq.OrderBy(m => m.User.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => m.UserId).ToListAsync();

            return Ok(new { company = new { Id = 0, Name = MainName }, items = await BuildPeople(mids), total = mtotal, page, pageSize });
        }

        var company = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Id, c.Name, OwnerUserId = c.UserId, OwnerName = c.User.Name })
            .FirstOrDefaultAsync();
        if (company == null) return NotFound(new { message = "Company not found." });

        var q = ApplySearch(_db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId), s);
        var total = await q.CountAsync();

        var ids = await q.OrderBy(e => e.User.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => e.UserId).ToListAsync();

        // the owner is the first row of page 1
        if (page == 1 && (s.Length == 0 || company.OwnerName.ToLower().Contains(s) || "owner".Contains(s)))
            ids.Insert(0, company.OwnerUserId);

        return Ok(new { company, items = await BuildPeople(ids), total, page, pageSize });
    }

    // GET api/main/reporting-managers/tree                -> main team + every company
    // GET api/main/reporting-managers/tree?companyId=1    -> one company   (companyId=0 -> main team only)
    [HttpGet("tree")]
    public async Task<IActionResult> Tree([FromQuery] int? companyId)
    {
        const int Cap = 3000;

        if (companyId == 0)
        {
            var mids = await _db.MainEmployees.AsNoTracking()
                .OrderBy(m => m.User.Name).Select(m => m.UserId).Take(2000).ToListAsync();
            return Ok(new { company = new { Id = 0, Name = MainName }, people = await BuildPeople(mids), truncated = false });
        }

        if (companyId.HasValue)
        {
            var company = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == companyId.Value)
                .Select(c => new { c.Id, c.Name, OwnerUserId = c.UserId, OwnerName = c.User.Name })
                .FirstOrDefaultAsync();
            if (company == null) return NotFound(new { message = "Company not found." });

            var one = await _db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == companyId.Value)
                .OrderBy(e => e.User.Name)
                .Select(e => e.UserId).Take(2000).ToListAsync();
            one.Insert(0, company.OwnerUserId);

            return Ok(new { company, people = await BuildPeople(one), truncated = false });
        }

        // everything: the main team first, then each owner followed by that company's employees
        var ids = await _db.MainEmployees.AsNoTracking()
            .OrderBy(m => m.User.Name).Select(m => m.UserId).ToListAsync();

        var owners = await _db.Companies.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.UserId })
            .ToListAsync();

        var emps = await _db.Employees.AsNoTracking()
            .OrderBy(e => e.Company.Name).ThenBy(e => e.User.Name)
            .Select(e => new { e.UserId, e.CompanyId })
            .Take(Cap)
            .ToListAsync();

        var byCompany = emps.GroupBy(e => e.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToList());

        foreach (var o in owners)
        {
            ids.Add(o.UserId);
            if (byCompany.TryGetValue(o.Id, out var list)) ids.AddRange(list);
        }

        return Ok(new { company = (object?)null, people = await BuildPeople(ids), truncated = emps.Count >= Cap });
    }

    // GET api/main/reporting-managers/person/12   (userId)
    [HttpGet("person/{userId:int}")]
    public async Task<IActionResult> Person(int userId)
    {
        var p = (await BuildPeople(new List<int> { userId })).FirstOrDefault();
        return p == null ? NotFound(new { message = "Person not found." }) : Ok(p);
    }

    // GET api/main/reporting-managers/connections/12   (userId)
    [HttpGet("connections/{userId:int}")]
    public async Task<IActionResult> Connections(int userId)
    {
        var self = (await BuildPeople(new List<int> { userId })).FirstOrDefault();
        if (self == null) return NotFound(new { message = "Person not found." });

        // everyone who lists this person as a manager (any company, or the main team)
        var reportIds = await _db.ReportingManagers.AsNoTracking()
            .Where(r => r.ManagerUserId == userId)
            .Select(r => r.UserId)
            .ToListAsync();

        // an owner is also the automatic manager of every employee of the company
        if (self.IsOwner)
        {
            reportIds.AddRange(await _db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == self.CompanyId && e.IsActive)
                .Select(e => e.UserId)
                .ToListAsync());
        }
        // a main employee also manages the employees of the companies whose owner reports to them, minus exclusions
        if (self.IsMain)
        {
            reportIds.AddRange(await _db.Employees.AsNoTracking()
                .Where(e => e.IsActive &&
                            _db.ReportingManagers.Any(r => r.ManagerUserId == userId && r.UserId == e.Company.UserId) &&
                            !_db.ReportingExclusions.Any(x => x.UserId == e.UserId && x.ManagerUserId == userId))
                .Select(e => e.UserId)
                .ToListAsync());
        }
        reportIds = reportIds.Distinct().ToList();

        var pageIds = await _db.Users.AsNoTracking()
            .Where(u => reportIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Take(100)
            .Select(u => u.Id)
            .ToListAsync();

        var reports = await BuildPeople(pageIds);

        // chosen managers + the automatic company owner + inherited main managers
        var managers = new List<ReportingUserInfo>(self.Managers);
        if (self.DefaultManager != null) managers.Add(self.DefaultManager);
        managers.AddRange(self.InheritedMain);

        return Ok(new
        {
            person = self,
            managers,
            defaultManagerId = self.DefaultManager?.UserId,
            inheritedIds = self.InheritedMain.Select(m => m.UserId).ToList(),
            reports,
            reportsTotal = reportIds.Count
        });

    }

    // GET api/main/reporting-managers/candidates?companyId=1&search=&excludeUserId=12   (companyId 0 = main team)
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(
        [FromQuery] int companyId, [FromQuery] string? search, [FromQuery] int? excludeUserId)
    {
        var s = (search ?? string.Empty).Trim().ToLower();

        if (companyId == 0)
        {
            var mq = _db.MainEmployees.AsNoTracking().Where(m => m.IsActive);
            if (excludeUserId.HasValue) mq = mq.Where(m => m.UserId != excludeUserId.Value);
            mq = ApplyMainSearch(mq, s);

            var mids = await mq.OrderBy(m => m.User.Name).Take(30).Select(m => m.UserId).ToListAsync();
            var minfo = await LoadInfo(mids);
            return Ok(mids.Where(minfo.ContainsKey).Select(i => minfo[i]).ToList());
        }

        var company = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId && c.IsActive)
            .Select(c => new { c.UserId, OwnerName = c.User.Name })
            .FirstOrDefaultAsync();
        if (company == null) return NotFound(new { message = "Company not found." });

        // an owner can't report to their own company's people, so offer nobody
        if (excludeUserId.HasValue && excludeUserId.Value == company.UserId)
            return Ok(new List<ReportingUserInfo>());

        var ids = new List<int>();

        if (company.UserId != excludeUserId &&
            (s.Length == 0 || company.OwnerName.ToLower().Contains(s) || "owner".Contains(s)))
            ids.Add(company.UserId);

        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId && e.IsActive);
        if (excludeUserId.HasValue) q = q.Where(e => e.UserId != excludeUserId.Value);
        q = ApplySearch(q, s);

        ids.AddRange(await q.OrderBy(e => e.User.Name).Take(30).Select(e => e.UserId).ToListAsync());

        var info = await LoadInfo(ids);
        return Ok(ids.Distinct().Where(info.ContainsKey).Select(i => info[i]).ToList());
    }

    // ---------- writes ----------

    // POST api/main/reporting-managers/12/managers   { "managerUserId": 7 }
    [HttpPost("{userId:int}/managers")]
    public async Task<IActionResult> Add(int userId, [FromBody] AddReportingManagerRequest req)
    {
        if (!await ReportingRules.IsParticipantAsync(_db, userId))
            return NotFound(new { message = "Person not found." });

        var current = await _db.ReportingManagers
            .Where(r => r.UserId == userId).Select(r => r.ManagerUserId).ToListAsync();

        if (current.Contains(req.ManagerUserId)) return Conflict(new { message = "Already a reporting manager." });
        if (current.Count >= ReportingRules.MaxManagers) return Bad($"Max {ReportingRules.MaxManagers} reporting managers.");

        var isMain = await ReportingRules.IsMainAsync(_db, userId);
        var err = await ReportingRules.ValidateAsync(_db, userId, new List<int> { req.ManagerUserId }, isMain);
        if (err != null) return Bad(err);

        _db.ReportingManagers.Add(new ReportingManager { UserId = userId, ManagerUserId = req.ManagerUserId });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Already a reporting manager." }); }

        return Ok((await BuildPeople(new List<int> { userId })).FirstOrDefault());
    }

    // DELETE api/main/reporting-managers/12/managers/7
    [HttpDelete("{userId:int}/managers/{managerUserId:int}")]
    public async Task<IActionResult> Remove(int userId, int managerUserId)
    {
        var link = await _db.ReportingManagers
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ManagerUserId == managerUserId);
        if (link == null) return NotFound(new { message = "Link not found." });

        _db.ReportingManagers.Remove(link);

        // if this was a company owner, the automatic access ends, so its exclusions are no longer needed
        var ownedCompanyId = await _db.Companies.Where(c => c.UserId == userId)
            .Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (ownedCompanyId != null)
            _db.ReportingExclusions.RemoveRange(_db.ReportingExclusions.Where(x =>
                x.ManagerUserId == managerUserId &&
                _db.Employees.Any(e => e.UserId == x.UserId && e.CompanyId == ownedCompanyId)));

        await _db.SaveChangesAsync();

        return Ok((await BuildPeople(new List<int> { userId })).FirstOrDefault());
    }

    // POST api/main/reporting-managers/12/exclusions   { "managerUserId": 7 }
    // block a main employee's automatic access to ONE company employee
    [HttpPost("{userId:int}/exclusions")]
    public async Task<IActionResult> Exclude(int userId, [FromBody] AddReportingManagerRequest req)
    {
        var emp = await _db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => new { OwnerUserId = e.Company.UserId })
            .FirstOrDefaultAsync();
        if (emp == null) return NotFound(new { message = "Employee not found." });

        var inherits = await _db.MainEmployees.AnyAsync(m => m.UserId == req.ManagerUserId) &&
                       await _db.ReportingManagers.AnyAsync(r => r.UserId == emp.OwnerUserId && r.ManagerUserId == req.ManagerUserId);
        if (!inherits)
            return Bad("This main employee does not manage the company owner, so there is no automatic access to block.");

        if (!await _db.ReportingExclusions.AnyAsync(x => x.UserId == userId && x.ManagerUserId == req.ManagerUserId))
        {
            _db.ReportingExclusions.Add(new ReportingExclusion { UserId = userId, ManagerUserId = req.ManagerUserId });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* already excluded */ }
        }

        return Ok((await BuildPeople(new List<int> { userId })).FirstOrDefault());
    }

    // DELETE api/main/reporting-managers/12/exclusions/7   (restore the automatic access)
    [HttpDelete("{userId:int}/exclusions/{managerUserId:int}")]
    public async Task<IActionResult> Restore(int userId, int managerUserId)
    {
        var row = await _db.ReportingExclusions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ManagerUserId == managerUserId);

        if (row != null)
        {
            _db.ReportingExclusions.Remove(row);
            await _db.SaveChangesAsync();
        }

        return Ok((await BuildPeople(new List<int> { userId })).FirstOrDefault());
    }
}