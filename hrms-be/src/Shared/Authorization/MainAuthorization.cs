using System.Security.Claims;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.Shared.Authorization;

// [RequireMainPermission("a", "b")]   -> needs a OR b
// two attributes on one action        -> needs both (AND)
// super admin always passes
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireMainPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _slugs;
    public RequireMainPermissionAttribute(params string[] slugs) => _slugs = slugs;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) { ctx.Result = new UnauthorizedResult(); return; }

        if (!int.TryParse(user.FindFirstValue("uid"), out var uid)) { ctx.Result = new UnauthorizedResult(); return; }

        var svc = ctx.HttpContext.RequestServices.GetRequiredService<MainPermissionService>();
        if (!await svc.HasAnyAsync(uid, _slugs))
            ctx.Result = new ObjectResult(new { message = "You do not have permission to do this." }) { StatusCode = 403 };
    }
}

// super admin only (permission management, destructive actions)
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSuperAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) { ctx.Result = new UnauthorizedResult(); return; }

        if (!int.TryParse(user.FindFirstValue("uid"), out var uid)) { ctx.Result = new UnauthorizedResult(); return; }

        var svc = ctx.HttpContext.RequestServices.GetRequiredService<MainPermissionService>();
        if (!await svc.IsSuperAdminAsync(uid))
            ctx.Result = new ObjectResult(new { message = "Only the super admin can do this." }) { StatusCode = 403 };
    }
}