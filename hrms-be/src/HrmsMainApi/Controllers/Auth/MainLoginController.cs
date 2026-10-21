using BCrypt.Net;
using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record MainLoginRequest(string Email, string Password, bool Remember);

[ApiController]
[Route("api/main/auth")]
public class MainLoginController : BaseController
{
    private const string CookieName = "access_token";

    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly MainPermissionService _perm;

    public MainLoginController(AppDbContext db, JwtService jwt, MainPermissionService perm)
    {
        _db = db;
        _jwt = jwt;
        _perm = perm;
    }

    // super admin: an active main role. main team member: active record + active role.
    private async Task<string?> ResolveRoleAsync(hrms.Models.User user)
    {
        if (user.MainRole != null && user.MainRole.IsActive) return user.MainRole.Slug;

        var me = await _db.MainEmployees.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => new { m.IsActive, RoleActive = m.Role.IsActive, m.Role.Slug })
            .FirstOrDefaultAsync();

        return me != null && me.IsActive && me.RoleActive ? me.Slug : null;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] MainLoginRequest req)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.MainRole)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "Your account is deactivated. Contact the administrator." });

        var role = await ResolveRoleAsync(user);
        if (role == null)
            return StatusCode(403, new { message = "Access restricted. Only main team members can sign in here." });

        var (token, expiresAt) = _jwt.CreateToken(user, role);

        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = req.Remember ? (DateTimeOffset?)expiresAt : null
        });

        var (isSuper, permissions) = await _perm.LoadAsync(user.Id);

        return Ok(new
        {
            expiresAt,
            user = new { id = user.Id, name = user.Name, email = user.Email, role, isSuperAdmin = isSuper, permissions }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users
            .Include(u => u.MainRole)
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        if (user == null || !user.IsActive) return Unauthorized();

        var role = await ResolveRoleAsync(user);
        if (role == null) return Unauthorized();

        var (isSuper, permissions) = await _perm.LoadAsync(user.Id);

        return Ok(new { id = user.Id, name = user.Name, email = user.Email, role, isSuperAdmin = isSuper, permissions });
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
}