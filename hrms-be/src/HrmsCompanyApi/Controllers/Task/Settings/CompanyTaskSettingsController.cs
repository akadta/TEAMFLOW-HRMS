using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record CompanyTaskSettingInput(bool IsActive, bool ApprovalRequired, LeaveConfig? Config);

[Route("api/company/tasks/settings")]
public class CompanyTaskSettingsController : CompanyBaseController
{
    private readonly AppDbContext _db;

    public CompanyTaskSettingsController(AppDbContext db) => _db = db;

    private IActionResult? OwnerOnly() =>
        IsCompanyOwner ? null : StatusCode(403, new { message = "Only the company owner can change task settings." });

    private Task<bool> IsMine(int userId) =>
        _db.Employees.AnyAsync(e => e.UserId == userId && e.CompanyId == CurrentCompanyId);

    // GET ?userId=   (omit = the whole company, userId = one employee)
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? userId)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;
        if (userId != null && !await IsMine(userId.Value)) return NotFound(new { message = "Employee not found." });

        var cid = CurrentCompanyId;
        var s = await _db.TaskSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == cid && x.UserId == userId);
        return Ok(new
        {
            isActive = s?.IsActive ?? false,
            approvalRequired = s?.ApprovalRequired ?? false,
            config = LeaveFlow.Parse(s?.Config),
            hasOwn = s != null
        });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromQuery] int? userId, [FromBody] CompanyTaskSettingInput i)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;
        if (userId != null && !await IsMine(userId.Value)) return NotFound(new { message = "Employee not found." });

        var cfg = i.Config ?? new LeaveConfig();
        cfg.Mode = cfg.Mode == "all" ? "all" : "any";
        if (i.ApprovalRequired && !cfg.Managers && !cfg.Owner && !cfg.Main)
            return BadRequest(new { message = "Tick at least one approver." });

        var cid = CurrentCompanyId;
        var s = await _db.TaskSettings.FirstOrDefaultAsync(x => x.CompanyId == cid && x.UserId == userId);
        if (s == null)
        {
            s = new TaskSetting { CompanyId = cid, UserId = userId };
            _db.TaskSettings.Add(s);
        }

        s.IsActive = i.IsActive;
        s.ApprovalRequired = i.ApprovalRequired;
        s.Config = LeaveFlow.Serialize(cfg);
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int userId)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var cid = CurrentCompanyId;
        var s = await _db.TaskSettings.FirstOrDefaultAsync(x => x.CompanyId == cid && x.UserId == userId);
        if (s != null)
        {
            _db.TaskSettings.Remove(s);
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    // GET employees?roleId=&search=   (picker for employee-wise settings)
    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] int? roleId, [FromQuery] string? search)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var cid = CurrentCompanyId;
        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid && e.IsActive);
        if (roleId != null) q = q.Where(e => e.CompanyRoleId == roleId);

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0) q = q.Where(e => e.User.Name.ToLower().Contains(s) || e.EmployeeCode.ToLower().Contains(s));

        var list = await q.OrderBy(e => e.User.Name).Take(200)
            .Select(e => new
            {
                e.UserId,
                Code = e.EmployeeCode,
                Name = e.User.Name,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                HasOverride = _db.TaskSettings.Any(x => x.CompanyId == cid && x.UserId == e.UserId),
                OverrideActive = _db.TaskSettings.Any(x => x.CompanyId == cid && x.UserId == e.UserId && x.IsActive)
            })
            .ToListAsync();

        return Ok(list);
    }
}