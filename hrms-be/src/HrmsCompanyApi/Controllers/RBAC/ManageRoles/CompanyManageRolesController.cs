using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record ManageRoleInput(string? Name);
public record ManageRoleStatus(bool IsActive);

[Route("api/company/roles")]
public class CompanyManageRolesController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly PermissionService _perm;

    public CompanyManageRolesController(AppDbContext db, PermissionService perm)
    {
        _db = db;
        _perm = perm;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private async Task<bool> Can() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("company.role");

    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static string Slugify(string v) => Regex.Replace(v.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private static string? Check(string name, string slug)
    {
        if (name.Length == 0) return "Role name is required.";
        if (name.Length > 100) return "Role name: max 100 characters.";
        if (slug.Length == 0) return "Use letters or numbers in the role name.";
        if (slug == "owner") return "\"Owner\" is a reserved name.";
        return null;
    }

    private static object Shape(CompanyRole r, int employees) =>
        new { r.Id, r.Name, r.Slug, r.IsActive, Employees = employees };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!await Can()) return Forbid();

        var cid = CurrentCompanyId;
        var items = await _db.CompanyRoles.AsNoTracking()
            .Where(r => r.CompanyId == cid)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Slug,
                r.IsActive,
                Employees = _db.Employees.Count(e => e.CompanyRoleId == r.Id)
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ManageRoleInput i)
    {
        if (!await Can()) return Forbid();

        var cid = CurrentCompanyId;
        var name = Clean(i.Name);
        var slug = Slugify(name);
        var err = Check(name, slug);
        if (err != null) return Bad(err);

        if (await _db.CompanyRoles.AnyAsync(r => r.CompanyId == cid && r.Slug == slug))
            return Bad("This role already exists.");

        var role = new CompanyRole { CompanyId = cid, Name = name, Slug = slug, IsActive = true };
        _db.CompanyRoles.Add(role);

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "This role already exists." }); }

        return Ok(Shape(role, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ManageRoleInput i)
    {
        if (!await Can()) return Forbid();

        var cid = CurrentCompanyId;
        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == cid);
        if (role == null) return NotFound(new { message = "Role not found." });

        var name = Clean(i.Name);
        var slug = Slugify(name);
        var err = Check(name, slug);
        if (err != null) return Bad(err);

        if (await _db.CompanyRoles.AnyAsync(r => r.CompanyId == cid && r.Slug == slug && r.Id != id))
            return Bad("This role already exists.");

        role.Name = name;
        role.Slug = slug;
        role.UpdatedAt = DateTime.UtcNow;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "This role already exists." }); }

        return Ok(Shape(role, await _db.Employees.CountAsync(e => e.CompanyRoleId == id)));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] ManageRoleStatus i)
    {
        if (!await Can()) return Forbid();

        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == CurrentCompanyId);
        if (role == null) return NotFound(new { message = "Role not found." });

        role.IsActive = i.IsActive;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = role.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await Can()) return Forbid();

        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == CurrentCompanyId);
        if (role == null) return NotFound(new { message = "Role not found." });

        var used = await _db.Employees.CountAsync(e => e.CompanyRoleId == id);
        if (used > 0) return Bad($"{used} employee(s) use this role. Move them to another role first.");

        _db.CompanyRoles.Remove(role);
        await _db.SaveChangesAsync();

        return Ok();
    }
}