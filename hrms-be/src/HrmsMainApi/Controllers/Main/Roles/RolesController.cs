using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record RoleSaveRequest(string? Name, bool? IsActive);
public record RoleStatusRequest(bool IsActive);

[ApiController]
[Authorize]
[Route("api/main/roles")]
public class RolesController : BaseController
{
    private readonly AppDbContext _db;
    public RolesController(AppDbContext db) => _db = db;

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });
    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static string Slugify(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Roles.AsNoTracking().Where(r => r.Slug != "company").OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Slug,
                r.IsActive,
                MainEmployees = _db.MainEmployees.Count(m => m.RoleId == r.Id),
                Users = _db.Users.Count(u => u.RoleId == r.Id)
            })
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RoleSaveRequest req)
    {
        var name = Clean(req.Name);
        if (name.Length < 2 || name.Length > 60) return Bad("Role name: 2-60 characters.");

        var slug = Slugify(name);
        if (slug.Length == 0) return Bad("Role name needs letters or numbers.");

        if (await _db.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower() || r.Slug == slug))
            return Conflict(new { message = "A role with this name already exists." });

        var role = new Role { Name = name, Slug = slug, IsActive = req.IsActive ?? true };
        _db.Roles.Add(role);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "A role with this name already exists." }); }

        return Ok(new { role.Id, role.Name, role.Slug, role.IsActive, MainEmployees = 0, Users = 0 });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RoleSaveRequest req)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        var name = Clean(req.Name);
        if (name.Length < 2 || name.Length > 60) return Bad("Role name: 2-60 characters.");
        if (await _db.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "A role with this name already exists." });

        role.Name = name;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { role.Id, role.Name, role.Slug, role.IsActive });
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] RoleStatusRequest req)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        role.IsActive = req.IsActive;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { isActive = role.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        if (await _db.MainEmployees.AnyAsync(m => m.RoleId == id) || await _db.Users.AnyAsync(u => u.RoleId == id))
            return Conflict(new { message = "This role is in use. Deactivate it instead." });

        _db.Roles.Remove(role);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "This role is in use. Deactivate it instead." }); }

        return Ok();
    }
}