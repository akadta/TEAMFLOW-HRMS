using hrms.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hrms.Shared.Authorization;

namespace hrms.HrmsCompanyApi.Controllers;

public record CompanyEmployeeStatusRequest(bool IsActive);

[Route("api/company/employee-list")]
public class CompanyEmployeeListController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CompanyEmployeeListController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private IActionResult? OwnerOnly() =>
        IsCompanyOwner ? null : StatusCode(403, new { message = "Only the company owner can do this." });

    // GET api/company/employee-list?search=&status=active|inactive&page=1&pageSize=50
    [HttpGet]
    [RequirePermission("employees.list")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var cid = CurrentCompanyId;

        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid);

        if (status == "active") q = q.Where(e => e.IsActive);
        else if (status == "inactive") q = q.Where(e => !e.IsActive);

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0)
        {
            q = q.Where(e =>
                e.User.Name.ToLower().Contains(s) ||
                e.User.Email.ToLower().Contains(s) ||
                (e.User.PhoneNumber ?? "").Contains(s) ||
                e.EmployeeCode.ToLower().Contains(s) ||
                (e.CompanyRole != null && e.CompanyRole.Name.ToLower().Contains(s)));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(e => e.User.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("employees.list")]
    public async Task<IActionResult> Get(int id)
    {
        var cid = CurrentCompanyId;
        var d = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == cid)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.EmployeeCode,
                Name = e.User.Name,
                Email = e.User.Email,
                Phone = e.User.PhoneNumber,
                e.EmergencyNumber,
                e.OfficialEmail,
                e.DateOfBirth,
                e.Gender,
                e.BloodGroup,
                e.MaritalStatus,
                e.Nationality,
                e.AadharNumber,
                e.PanNumber,
                e.Address,
                e.ProfilePicture,
                e.HighestQualification,
                e.Institution,
                e.YearOfPassing,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                e.WorkType,
                e.IsActive,
                e.CreatedAt,
                Documents = e.Documents.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.Type, x.Title, x.Description, x.FilePath }),
                Histories = e.Histories.OrderByDescending(x => x.StartDate)
                    .Select(x => new { x.Id, x.Employer, x.StartDate, x.EndDate, x.ReasonForLeaving, x.RelievingLetter })
            })
            .FirstOrDefaultAsync();

        if (d == null) return NotFound(new { message = "Employee not found." });
        return Ok(d);
    }

    [HttpPatch("{id:int}/status")]
    [RequirePermission("employees.edit")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] CompanyEmployeeStatusRequest req)
    {

        var cid = CurrentCompanyId;
        var e = await _db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == cid);
        if (e == null) return NotFound(new { message = "Employee not found." });
        if (e.UserId == CurrentUserId) return BadRequest(new { message = "You cannot change your own status." });

        e.IsActive = req.IsActive;
        e.User.IsActive = req.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.User.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = e.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var cid = CurrentCompanyId;
        var e = await _db.Employees
            .Include(x => x.User)
            .Include(x => x.Documents)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == cid);

        if (e == null) return NotFound(new { message = "Employee not found." });
        if (e.UserId == CurrentUserId) return BadRequest(new { message = "You cannot delete yourself." });

        var paths = new List<string?> { e.ProfilePicture };
        paths.AddRange(e.Documents.Select(x => x.FilePath));
        paths.AddRange(e.Histories.Select(x => x.RelievingLetter));

        _db.Employees.Remove(e);
        _db.Users.Remove(e.User);
        await _db.SaveChangesAsync();

        foreach (var p in paths) CompanyEmployeeRules.DeleteOld(_env, p);

        return Ok();
    }
}