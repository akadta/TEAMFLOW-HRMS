using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record SetEmployeePermissionsRequest(List<int>? PermissionIds);

[Route("api/company/permissions")]
public class CompanyEmployeePermissionsController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly PermissionService _perm;

    public CompanyEmployeePermissionsController(AppDbContext db, PermissionService perm)
    {
        _db = db;
        _perm = perm;
    }

    // owner always; others need the "role.permission" slug
    private async Task<IActionResult?> Guard()
    {
        if (IsCompanyOwner) return null;

        var ok = (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("role.permission");
        return ok ? null : StatusCode(403, new { message = "You do not have permission to manage permissions." });
    }

    // GET api/company/permissions/catalog
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        var deny = await Guard(); if (deny != null) return deny;

        var list = await _db.PermissionHeadings.AsNoTracking()
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

    // GET api/company/permissions/employees
    [HttpGet("employees")]
    public async Task<IActionResult> Employees()
    {
        var deny = await Guard(); if (deny != null) return deny;
        var cid = CurrentCompanyId;

        var list = await _db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == cid && e.IsActive)
            .OrderBy(e => e.User.Name)
            .Select(e => new
            {
                e.Id,
                e.EmployeeCode,
                Name = e.User.Name,
                Email = e.User.Email,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null
            })
            .ToListAsync();
        return Ok(list);
    }

    // GET api/company/permissions/employee/5
    [HttpGet("employee/{employeeId:int}")]
    public async Task<IActionResult> ForEmployee(int employeeId)
    {
        var deny = await Guard(); if (deny != null) return deny;
        var cid = CurrentCompanyId;

        if (!await _db.Employees.AnyAsync(e => e.Id == employeeId && e.CompanyId == cid))
            return NotFound(new { message = "Employee not found." });

        var ids = await _db.EmployeePermissions.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => x.PermissionId)
            .ToListAsync();
        return Ok(new { permissionIds = ids });
    }

    // PUT api/company/permissions/employee/5   body: { "permissionIds": [1,2,3] }
    [HttpPut("employee/{employeeId:int}")]
    public async Task<IActionResult> Set(int employeeId, [FromBody] SetEmployeePermissionsRequest req)
    {
        var deny = await Guard(); if (deny != null) return deny;
        var cid = CurrentCompanyId;

        // a non-owner cannot grant themselves more access
        if (!IsCompanyOwner && employeeId == CurrentEmployeeId)
            return StatusCode(403, new { message = "You cannot change your own permissions." });

        if (!await _db.Employees.AnyAsync(e => e.Id == employeeId && e.CompanyId == cid))
            return NotFound(new { message = "Employee not found." });

        var want = (req.PermissionIds ?? new()).Distinct().ToList();
        var valid = await _db.Permissions
            .Where(p => want.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();
        if (valid.Count != want.Count)
            return BadRequest(new { message = "Some permissions are invalid." });

        var current = await _db.EmployeePermissions.Where(x => x.EmployeeId == employeeId).ToListAsync();
        _db.EmployeePermissions.RemoveRange(current.Where(c => !want.Contains(c.PermissionId)));

        var have = current.Select(c => c.PermissionId).ToHashSet();
        _db.EmployeePermissions.AddRange(
            want.Where(id => !have.Contains(id))
                .Select(id => new EmployeePermission { EmployeeId = employeeId, PermissionId = id }));

        await _db.SaveChangesAsync();
        return Ok(new { count = want.Count });
    }
}