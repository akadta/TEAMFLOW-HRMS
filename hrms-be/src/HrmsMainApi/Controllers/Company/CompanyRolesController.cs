using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record CompanyRoleCreateRequest(int CompanyId, string? Name);
public record CompanyRoleUpdateRequest(string? Name);
public record CompanyRoleStatusRequest(bool IsActive);

[ApiController]
[Authorize]
[Route("api/main/company-roles")]
public class CompanyRolesController : BaseController
{
    private readonly AppDbContext _db;

    public CompanyRolesController(AppDbContext db)
    {
        _db = db;
    }

    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static string Slugify(string v) => Regex.Replace(v.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private static object Shape(CompanyRole r) => new { r.Id, r.CompanyId, r.Name, r.Slug, r.IsActive };

    // returns an error message, or null when the name is fine
    private static string? CheckName(string name, string slug)
    {
        if (name.Length == 0) return "Role name is required.";
        if (name.Length > 100) return "Max 100 characters.";
        if (slug.Length == 0) return "Use letters or numbers in the role name.";
        return null;
    }

    // dropdown data: all companies, light payload
    [HttpGet("companies")]
    public async Task<IActionResult> Companies()
    {
        var items = await _db.Companies
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToListAsync();

        return Ok(items);
    }

    // GET api/main/company-roles?companyId=1
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int companyId)
    {
        var items = await _db.CompanyRoles
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.CompanyId, r.Name, r.Slug, r.IsActive })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompanyRoleCreateRequest req)
    {
        if (!await _db.Companies.AnyAsync(c => c.Id == req.CompanyId))
            return NotFound(new { message = "Company not found." });

        var name = Clean(req.Name);
        var slug = Slugify(name);
        var problem = CheckName(name, slug);
        if (problem != null) return BadRequest(new { message = problem });

        if (await _db.CompanyRoles.AnyAsync(r => r.CompanyId == req.CompanyId && r.Slug == slug))
            return BadRequest(new { message = "This role already exists for the company." });

        var role = new CompanyRole
        {
            CompanyId = req.CompanyId,
            Name = name,
            Slug = slug,
            IsActive = true
        };
        _db.CompanyRoles.Add(role);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "This role already exists for the company." });
        }

        return Ok(Shape(role));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompanyRoleUpdateRequest req)
    {
        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        var name = Clean(req.Name);
        var slug = Slugify(name);
        var problem = CheckName(name, slug);
        if (problem != null) return BadRequest(new { message = problem });

        if (await _db.CompanyRoles.AnyAsync(r => r.CompanyId == role.CompanyId && r.Slug == slug && r.Id != id))
            return BadRequest(new { message = "This role already exists for the company." });

        role.Name = name;
        role.Slug = slug;
        role.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "This role already exists for the company." });
        }

        return Ok(Shape(role));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] CompanyRoleStatusRequest req)
    {
        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        role.IsActive = req.IsActive;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = role.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.CompanyRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "Role not found." });

        _db.CompanyRoles.Remove(role);
        await _db.SaveChangesAsync();

        return Ok();
    }
}