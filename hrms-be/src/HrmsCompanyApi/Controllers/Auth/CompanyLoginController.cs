using System.Security.Claims;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record CompanyLoginRequest(string Email, string Password, bool Remember, double? Lat = null, double? Lng = null, double? Accuracy = null);

[ApiController]
[Route("api/company/auth")]
public class CompanyLoginController : BaseController
{
    private const string CookieName = "company_access_token";

    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly PermissionService _perm;
    private readonly AttendanceRules _rules;

    public CompanyLoginController(AppDbContext db, JwtService jwt, PermissionService perm, AttendanceRules rules)
    {
        _db = db;
        _jwt = jwt;
        _perm = perm;
        _rules = rules;
    }

    private sealed record Access(Company Company, Employee? Employee, string Role, string RoleName, string Type);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] CompanyLoginRequest req)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "Your account is deactivated. Contact your administrator." });

        var (access, error) = await ResolveAccessAsync(user);
        if (access == null)
            return error!;

        // rule "location at every login": no location = no sign in
        var rs = await _rules.LoadAsync(access.Company.Id, user.Id);
        if (rs.LoginLocation)
        {
            var okLoc = req.Lat is >= -90 and <= 90 && req.Lng is >= -180 and <= 180;
            if (!okLoc)
                return StatusCode(428, new { message = "Location is required to sign in.", locationRequired = true });

            _db.LoginLocations.Add(new LoginLocation
            {
                UserId = user.Id,
                CompanyId = access.Company.Id,
                Lat = req.Lat!.Value,
                Lng = req.Lng!.Value,
                Accuracy = req.Accuracy
            });
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new("cid", access.Company.Id.ToString()),
            new("type", access.Type)
        };
        if (access.Employee != null)
            claims.Add(new Claim("eid", access.Employee.Id.ToString()));

        var (token, expiresAt) = _jwt.CreateCompanyToken(user, access.Role, claims);
        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = req.Remember ? (DateTimeOffset?)expiresAt : null
        });

        return Ok(new { expiresAt, user = await ToDtoAsync(user, access) });
    }
    [Authorize(AuthenticationSchemes = "CompanyBearer")]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        if (user == null || !user.IsActive)
            return Unauthorized();

        var (access, _) = await ResolveAccessAsync(user);
        if (access == null)
            return Unauthorized();

        return Ok(await ToDtoAsync(user, access));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        return Ok();
    }

    // Decides whether this user is a company owner or an employee, and validates the company/role.
    private async Task<(Access? Access, IActionResult? Error)> ResolveAccessAsync(User user)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (company != null)
        {
            if (!company.IsActive)
                return (null, StatusCode(403, new { message = "This company account is deactivated." }));

            return (new Access(company, null, "owner", "Owner", "owner"), null);
        }

        var employee = await _db.Employees
            .Include(e => e.Company)
            .Include(e => e.CompanyRole)
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

        if (employee == null)
            return (null, StatusCode(403, new { message = "Access restricted. This account is not linked to a company." }));

        if (!employee.IsActive)
            return (null, StatusCode(403, new { message = "Your employee profile is deactivated. Contact your administrator." }));

        if (!employee.Company.IsActive)
            return (null, StatusCode(403, new { message = "Your company account is deactivated." }));

        if (employee.CompanyRole == null || !employee.CompanyRole.IsActive)
            return (null, StatusCode(403, new { message = "No active role is assigned to you. Contact your administrator." }));

        return (new Access(employee.Company, employee, employee.CompanyRole.Slug, employee.CompanyRole.Name, "employee"), null);
    }

    private async Task<object> ToDtoAsync(User user, Access a) => new
    {
        id = user.Id,
        name = user.Name,
        email = user.Email,
        type = a.Type,
        role = a.Role,
        roleName = a.RoleName,
        employeeId = a.Employee?.Id,
        employeeCode = a.Employee?.EmployeeCode,
        profilePicture = a.Employee?.ProfilePicture,
        company = new
        {
            id = a.Company.Id,
            name = a.Company.Name,
            logo = a.Company.Logo,
            theme = a.Company.Theme
        },
        permissions = await _perm.SlugsAsync(a.Type == "owner", a.Employee?.Id)
    };
}