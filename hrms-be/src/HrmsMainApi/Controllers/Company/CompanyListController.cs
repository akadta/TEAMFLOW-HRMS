using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record CompanyStatusRequest(bool IsActive);

[ApiController]
[Authorize]
[Route("api/main/companies")]
public class CompanyListController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CompanyListController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var q = _db.Companies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s)
                          || c.OwnerName.ToLower().Contains(s)
                          || c.User.Email.Contains(s)
                          || c.User.PhoneNumber!.Contains(s));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.OwnerName,
                Email = c.User.Email,
                PhoneNumber = c.User.PhoneNumber,
                c.WhatsappNumber,
                c.Logo,
                c.Theme,
                c.Address,
                c.Latitude,
                c.Longitude,
                c.About,
                c.IsActive,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromForm] CompanyForm f)
    {
        var company = await _db.Companies.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound(new { message = "Company not found." });

        var errors = CompanyRules.Validate(f, isCreate: false);
        if (errors.Count == 0)
            foreach (var kv in await CompanyRules.DuplicatesAsync(_db, f, id))
                errors[kv.Key] = kv.Value;

        if (errors.Count > 0)
            return BadRequest(new { message = "Please fix the highlighted fields.", errors });

        var root = CompanyRules.WebRoot(_env);
        string? newLogo = null;
        string? oldLogo = null;

        if (f.Logo != null)
        {
            newLogo = await CompanyRules.SaveLogoAsync(f.Logo, root);
            oldLogo = company.Logo;
            company.Logo = newLogo;
        }

        company.Name = CompanyRules.Name(f.CompanyName);
        company.OwnerName = CompanyRules.Name(f.OwnerName);
        company.Address = CompanyRules.NullIfEmpty(f.Address);
        company.Latitude = f.Latitude;
        company.Longitude = f.Longitude;
        company.About = CompanyRules.NullIfEmpty(f.About);
        company.WhatsappNumber = CompanyRules.NullIfEmpty(CompanyRules.Phone(f.WhatsappNumber));
        if (!string.IsNullOrWhiteSpace(f.Theme)) company.Theme = f.Theme;
        company.UpdatedAt = DateTime.UtcNow;

        company.User.Name = company.OwnerName;
        company.User.Email = CompanyRules.Email(f.Email);
        company.User.PhoneNumber = CompanyRules.Phone(f.PhoneNumber);
        company.User.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(); // company + user saved together
        }
        catch (DbUpdateException)
        {
            CompanyRules.DeleteLogo(newLogo, root);
            return Conflict(new { message = "Email, phone, company name or WhatsApp already exists." });
        }

        CompanyRules.DeleteLogo(oldLogo, root);
        return Ok();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] CompanyStatusRequest req)
    {
        var company = await _db.Companies.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound(new { message = "Company not found." });

        company.IsActive = req.IsActive;
        company.User.IsActive = req.IsActive;
        company.UpdatedAt = DateTime.UtcNow;
        company.User.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(); // single SaveChanges = one transaction

        return Ok(new { isActive = company.IsActive });
    }
}