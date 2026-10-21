using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace hrms.Shared.Controllers;

public abstract class BaseController : ControllerBase
{
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue("uid")!);

    protected string CurrentUserEmail =>
        User.FindFirstValue("sub")!;

    protected string CurrentUserName =>
        User.FindFirstValue("name")!;

    protected string CurrentUserRole =>
        User.FindFirstValue("role") ?? string.Empty;

    // ---- company app claims (only present on company tokens) ----

    protected int CurrentCompanyId =>
        int.Parse(User.FindFirstValue("cid") ?? "0");

    protected int? CurrentEmployeeId =>
        int.TryParse(User.FindFirstValue("eid"), out var id) ? id : null;

    protected bool IsCompanyOwner =>
        User.FindFirstValue("type") == "owner";
}