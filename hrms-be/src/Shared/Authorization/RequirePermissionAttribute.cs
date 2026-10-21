using System.Security.Claims;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.Shared.Authorization;

// [RequirePermission("a", "b")]                 -> needs a OR b
// two attributes on one action                  -> needs both (AND)
// the company owner always passes
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _slugs;
    public RequirePermissionAttribute(params string[] slugs) => _slugs = slugs;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) { ctx.Result = new UnauthorizedResult(); return; }
        if (user.FindFirstValue("type") == "owner") return;

        var eid = int.TryParse(user.FindFirstValue("eid"), out var id) ? id : (int?)null;
        var svc = ctx.HttpContext.RequestServices.GetRequiredService<PermissionService>();

        if (eid == null || !await svc.HasAnyAsync(eid.Value, _slugs))
            ctx.Result = new ObjectResult(new { message = "You do not have permission to do this." }) { StatusCode = 403 };
    }
}