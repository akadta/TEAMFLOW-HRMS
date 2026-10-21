using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record TaskSettingInput(bool IsActive, bool ApprovalRequired, LeaveConfig? Config);

[ApiController]
[Authorize]
[Route("api/main/task-settings")]
public class TaskSettingsController : BaseController
{
    private readonly AppDbContext _db;

    public TaskSettingsController(AppDbContext db) => _db = db;

    private async Task<IActionResult?> CheckScope(int? companyId, int? userId)
    {
        if (userId != null && companyId == null) return BadRequest(new { message = "Choose a company." });
        if (companyId != null && !await _db.Companies.AnyAsync(c => c.Id == companyId))
            return NotFound(new { message = "Company not found." });
        if (userId != null && !await _db.Employees.AnyAsync(e => e.UserId == userId && e.CompanyId == companyId))
            return NotFound(new { message = "Employee not found." });
        return null;
    }

    // GET ?companyId=&userId=   (none = global, companyId = company, both = one employee)
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? companyId, [FromQuery] int? userId)
    {
        var deny = await CheckScope(companyId, userId);
        if (deny != null) return deny;

        var s = await _db.TaskSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId);
        return Ok(new
        {
            isActive = s?.IsActive ?? false,
            approvalRequired = s?.ApprovalRequired ?? false,
            config = LeaveFlow.Parse(s?.Config),
            hasOwn = s != null
        });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromQuery] int? companyId, [FromQuery] int? userId, [FromBody] TaskSettingInput i)
    {
        var deny = await CheckScope(companyId, userId);
        if (deny != null) return deny;

        var cfg = i.Config ?? new LeaveConfig();
        cfg.Mode = cfg.Mode == "all" ? "all" : "any";
        if (i.ApprovalRequired && !cfg.Managers && !cfg.Owner && !cfg.Main)
            return BadRequest(new { message = "Tick at least one approver." });

        var s = await _db.TaskSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId);
        if (s == null)
        {
            s = new TaskSetting { CompanyId = companyId, UserId = userId };
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
    public async Task<IActionResult> Remove([FromQuery] int companyId, [FromQuery] int userId)
    {
        var s = await _db.TaskSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId);
        if (s != null)
        {
            _db.TaskSettings.Remove(s);
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    // GET employees?companyId=&roleId=&search=   (picker for employee-wise settings)
    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] int companyId, [FromQuery] int? roleId, [FromQuery] string? search)
    {
        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId && e.IsActive);
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
                HasOverride = _db.TaskSettings.Any(x => x.CompanyId == companyId && x.UserId == e.UserId),
                OverrideActive = _db.TaskSettings.Any(x => x.CompanyId == companyId && x.UserId == e.UserId && x.IsActive)
            })
            .ToListAsync();

        return Ok(list);
    }
}