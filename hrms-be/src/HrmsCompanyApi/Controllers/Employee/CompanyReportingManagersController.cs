using hrms.Data;
using hrms.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

[Route("api/company/reporting-managers")]
public class CompanyReportingManagersController : CompanyBaseController
{
    private readonly AppDbContext _db;

    public CompanyReportingManagersController(AppDbContext db) => _db = db;

    // GET api/company/reporting-managers/candidates?search=&excludeUserId=12
    // active employees of THIS company only
    [HttpGet("candidates")]
    [RequirePermission("employees.register", "employees.edit")]
    public async Task<IActionResult> Candidates([FromQuery] string? search, [FromQuery] int? excludeUserId)
    {
        var cid = CurrentCompanyId;
        var s = (search ?? string.Empty).Trim().ToLower();

        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid && e.IsActive);
        if (excludeUserId.HasValue) q = q.Where(e => e.UserId != excludeUserId.Value);

        if (s.Length > 0)
        {
            q = q.Where(e =>
                e.User.Name.ToLower().Contains(s) ||
                e.User.Email.ToLower().Contains(s) ||
                e.EmployeeCode.ToLower().Contains(s) ||
                (e.CompanyRole != null && e.CompanyRole.Name.ToLower().Contains(s)));
        }

        var list = await q.OrderBy(e => e.User.Name).Take(30)
            .Select(e => new
            {
                UserId = e.UserId,
                EmployeeId = e.Id,
                Name = e.User.Name,
                Email = e.User.Email,
                Code = e.EmployeeCode,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.ProfilePicture,
                e.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET api/company/reporting-managers/person/12   (userId)
    // the in-company managers of this employee
    [HttpGet("person/{userId:int}")]
    [RequirePermission("employees.edit")]
    public async Task<IActionResult> Person(int userId)
    {
        var cid = CurrentCompanyId;

        if (!await _db.Employees.AnyAsync(e => e.UserId == userId && e.CompanyId == cid))
            return NotFound(new { message = "Employee not found." });

        var managers = await _db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == cid &&
                        _db.ReportingManagers.Any(r => r.UserId == userId && r.ManagerUserId == e.UserId))
            .OrderBy(e => e.User.Name)
            .Select(e => new
            {
                UserId = e.UserId,
                EmployeeId = e.Id,
                Name = e.User.Name,
                Email = e.User.Email,
                Code = e.EmployeeCode,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.ProfilePicture,
                e.IsActive
            })
            .ToListAsync();

        return Ok(new { managers });
    }
}