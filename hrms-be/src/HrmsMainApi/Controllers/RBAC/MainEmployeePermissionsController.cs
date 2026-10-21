using hrms.Data;
using hrms.Models;
using hrms.Shared.Authorization;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record SetMainEmployeePermissionsRequest(List<int>? PermissionIds);

[ApiController]
[Authorize]
[RequireSuperAdmin]
[Route("api/main/main-permissions")]
public class MainEmployeePermissionsController : BaseController
{
    private readonly AppDbContext _db;
    public MainEmployeePermissionsController(AppDbContext db) => _db = db;

    // GET api/main/main-permissions/roles   (role filter dropdown)
    [HttpGet("roles")]
    public async Task<IActionResult> Roles()
    {
        var list = await _db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.IsActive })
            .ToListAsync();
        return Ok(list);
    }

    // GET api/main/main-permissions/catalog
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        var list = await _db.MainPermissionHeadings.AsNoTracking()
            .Where(h => h.IsActive)
            .OrderBy(h => h.SortOrder)
            .Select(h => new
            {
                h.Id,
                h.Name,
                h.Slug,
                Permissions = h.Permissions
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new { p.Id, p.Name, p.Slug })
            })
            .ToListAsync();
        return Ok(list);
    }

    // GET api/main/main-permissions/employees?roleId=3&search=ann
    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] int? roleId, [FromQuery] string? search)
    {
        var q = _db.MainEmployees.AsNoTracking().Where(m => m.IsActive);

        if (roleId != null) q = q.Where(m => m.RoleId == roleId);

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0)
            q = q.Where(m =>
                m.User.Name.ToLower().Contains(s) ||
                m.User.Email.ToLower().Contains(s) ||
                m.EmployeeCode.ToLower().Contains(s));

        var list = await q.OrderBy(m => m.User.Name)
            .Take(200)
            .Select(m => new
            {
                m.Id,
                m.EmployeeCode,
                Name = m.User.Name,
                Email = m.User.Email,
                m.RoleId,
                Role = m.Role.Name
            })
            .ToListAsync();
        return Ok(list);
    }

    // GET api/main/main-permissions/employee/5
    [HttpGet("employee/{id:int}")]
    public async Task<IActionResult> ForEmployee(int id)
    {
        if (!await _db.MainEmployees.AnyAsync(m => m.Id == id))
            return NotFound(new { message = "Main employee not found." });

        var ids = await _db.MainEmployeePermissions.AsNoTracking()
            .Where(x => x.MainEmployeeId == id)
            .Select(x => x.MainPermissionId)
            .ToListAsync();
        return Ok(new { permissionIds = ids });
    }

    // PUT api/main/main-permissions/employee/5   body: { "permissionIds": [1,2,3] }
    [HttpPut("employee/{id:int}")]
    public async Task<IActionResult> Set(int id, [FromBody] SetMainEmployeePermissionsRequest req)
    {
        if (!await _db.MainEmployees.AnyAsync(m => m.Id == id))
            return NotFound(new { message = "Main employee not found." });

        var want = (req.PermissionIds ?? new()).Distinct().ToList();
        var valid = await _db.MainPermissions
            .Where(p => want.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();
        if (valid.Count != want.Count)
            return BadRequest(new { message = "Some permissions are invalid." });

        var current = await _db.MainEmployeePermissions.Where(x => x.MainEmployeeId == id).ToListAsync();
        _db.MainEmployeePermissions.RemoveRange(current.Where(c => !want.Contains(c.MainPermissionId)));

        var have = current.Select(c => c.MainPermissionId).ToHashSet();
        _db.MainEmployeePermissions.AddRange(
            want.Where(pid => !have.Contains(pid))
                .Select(pid => new MainEmployeePermission { MainEmployeeId = id, MainPermissionId = pid }));

        await _db.SaveChangesAsync();
        return Ok(new { count = want.Count });
    }
}