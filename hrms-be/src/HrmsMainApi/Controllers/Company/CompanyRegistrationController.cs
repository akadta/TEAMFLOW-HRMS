using BCrypt.Net;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/companies")]
public class CompanyRegistrationController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CompanyRegistrationController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // inline duplicate check: GET check?field=email&value=a@b.com&excludeId=5
    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] string field, [FromQuery] string? value, [FromQuery] int? excludeId)
    {
        var f = new CompanyForm();
        switch (field)
        {
            case "companyName": f.CompanyName = value; break;
            case "email": f.Email = value; break;
            case "phoneNumber": f.PhoneNumber = value; break;
            case "whatsappNumber": f.WhatsappNumber = value; break;
            default: return BadRequest(new { message = "Unknown field." });
        }

        var dup = await CompanyRules.DuplicatesAsync(_db, f, excludeId);
        return Ok(new { taken = dup.Count > 0, message = dup.Values.FirstOrDefault() });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] CompanyForm f)
    {
        var errors = CompanyRules.Validate(f, isCreate: true);
        if (errors.Count == 0)
            foreach (var kv in await CompanyRules.DuplicatesAsync(_db, f, null))
                errors[kv.Key] = kv.Value;

        if (errors.Count > 0)
            return BadRequest(new { message = "Please fix the highlighted fields.", errors });

        var root = CompanyRules.WebRoot(_env);
        string? logo = f.Logo != null ? await CompanyRules.SaveLogoAsync(f.Logo, root) : null;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Slug == "company");
            if (role == null)
            {
                role = new Role { Name = "Company", Slug = "company" };
                _db.Roles.Add(role);
                await _db.SaveChangesAsync();
            }

            var user = new User
            {
                Name = CompanyRules.Name(f.OwnerName),
                Email = CompanyRules.Email(f.Email),
                PhoneNumber = CompanyRules.Phone(f.PhoneNumber),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(f.Password!),
                IsActive = true,
                RoleId = role.Id
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var company = new Company
            {
                UserId = user.Id,
                Name = CompanyRules.Name(f.CompanyName),
                OwnerName = CompanyRules.Name(f.OwnerName),
                Logo = logo,
                Theme = string.IsNullOrWhiteSpace(f.Theme) ? "{}" : f.Theme,
                Address = CompanyRules.NullIfEmpty(f.Address),
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                About = CompanyRules.NullIfEmpty(f.About),
                WhatsappNumber = CompanyRules.NullIfEmpty(CompanyRules.Phone(f.WhatsappNumber)),
                IsActive = true
            };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return Ok(new { id = company.Id });
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            CompanyRules.DeleteLogo(logo, root);
            return Conflict(new { message = "Email, phone, company name or WhatsApp already exists." });
        }
        catch
        {
            await tx.RollbackAsync();
            CompanyRules.DeleteLogo(logo, root);
            throw;
        }
    }
}