using System.Text.Json;
using hrms.Data;
using hrms.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hrms.Shared.Authorization;

namespace hrms.HrmsCompanyApi.Controllers;

public record CompanyEmployeeDocInput(string? Type, string? Title, string? Description, string? FileKey);
public record CompanyEmployeeHistInput(string? Employer, DateOnly? StartDate, DateOnly? EndDate, string? ReasonForLeaving, string? FileKey);
public record CompanyEmployeeRegisterRequest(
    string? EmployeeCode,
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? EmergencyNumber,
    string? OfficialEmail,
    DateOnly? DateOfBirth,
    string? Gender,
    string? BloodGroup,
    string? MaritalStatus,
    string? Nationality,
    string? AadharNumber,
    string? PanNumber,
    string? Address,
    string? HighestQualification,
    string? Institution,
    int? YearOfPassing,
    int? CompanyRoleId,
    string? WorkType,
    string? Password,
    string? ConfirmPassword,
    List<CompanyEmployeeDocInput>? Documents,
    List<CompanyEmployeeHistInput>? Histories,
    List<int>? ManagerUserIds);

[Route("api/company/employee-registration")]
public class CompanyEmployeeRegisterController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CompanyEmployeeRegisterController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // private IActionResult? OwnerOnly() =>
    //     IsCompanyOwner ? null : StatusCode(403, new { message = "Only the company owner can do this." });

    // GET api/company/employee-registration/roles
    [HttpGet("roles")]
    [RequirePermission("employees.register", "employees.edit")]
    public async Task<IActionResult> Roles()
    {
        var cid = CurrentCompanyId;
        var list = await _db.CompanyRoles.AsNoTracking()
            .Where(r => r.CompanyId == cid)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.IsActive })
            .ToListAsync();
        return Ok(list);
    }

    // GET api/company/employee-registration/next-code
    [HttpGet("next-code")]
    [RequirePermission("employees.register")]
    public async Task<IActionResult> NextCodeFor()
    {
        return Ok(new { code = await CompanyEmployeeRules.NextCodeAsync(_db, CurrentCompanyId) });
    }

    // multipart: "payload" (json) + files: profilePicture, doc_0.., hist_0..
    [HttpPost]
    [RequirePermission("employees.register")]

    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<IActionResult> Register([FromForm] string payload)
    {
        // var deny = OwnerOnly();
        // if (deny != null) return deny;

        CompanyEmployeeRegisterRequest? req;
        try { req = JsonSerializer.Deserialize<CompanyEmployeeRegisterRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var cid = CurrentCompanyId;
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid);
        if (company == null || !company.IsActive) return Bad("Company is not active.");

        var err = CompanyEmployeeRules.CheckCore(new EmpCore(
            req.EmployeeCode, req.Name, req.Email, req.PhoneNumber, req.EmergencyNumber, req.OfficialEmail,
            req.DateOfBirth, req.Gender, req.BloodGroup, req.MaritalStatus, req.Nationality, req.AadharNumber,
            req.PanNumber, req.Address, req.HighestQualification, req.Institution, req.YearOfPassing, req.WorkType),
            false, out var v);
        if (err != null) return Bad(err);

        if (req.CompanyRoleId != null)
        {
            var role = await _db.CompanyRoles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == req.CompanyRoleId && r.CompanyId == cid);
            if (role == null) return Bad("Role not found for this company.");
            if (!role.IsActive) return Bad("Selected role is inactive.");
        }

        var mgrIds = (req.ManagerUserIds ?? new()).Distinct().ToList();
        var mgrErr = await CompanyReportingRules.ValidateAsync(_db, null, mgrIds, cid);
        if (mgrErr != null) return Bad(mgrErr);

        var pwErr = CompanyEmployeeRules.CheckPassword(req.Password, req.ConfirmPassword);
        if (pwErr != null) return Bad(pwErr);

        var files = Request.Form.Files;
        var profile = files.GetFile("profilePicture");
        var pErr = CompanyEmployeeRules.CheckProfile(profile);
        if (pErr != null) return Bad(pErr);

        var docsIn = req.Documents ?? new();
        var histIn = req.Histories ?? new();
        if (docsIn.Count > 30) return Bad("Max 30 documents.");
        if (histIn.Count > 20) return Bad("Max 20 employment entries.");

        var docs = new List<(EmployeeDocument Row, IFormFile? File)>();
        for (var i = 0; i < docsIn.Count; i++)
        {
            var d = docsIn[i];
            var f = d.FileKey == null ? null : files.GetFile(d.FileKey);
            var e = CompanyEmployeeRules.CheckDoc(i, d.Title, d.Type, d.Description, f, out var dc);
            if (e != null) return Bad(e);
            docs.Add((new EmployeeDocument { Type = dc!.Type, Title = dc.Title, Description = dc.Desc }, f));
        }

        var hist = new List<(EmployeeHistory Row, IFormFile? File)>();
        for (var i = 0; i < histIn.Count; i++)
        {
            var h = histIn[i];
            var f = h.FileKey == null ? null : files.GetFile(h.FileKey);
            var e = CompanyEmployeeRules.CheckHist(i, h.Employer, h.StartDate, h.EndDate, h.ReasonForLeaving, f, out var hc);
            if (e != null) return Bad(e);
            hist.Add((new EmployeeHistory { Employer = hc!.Employer, StartDate = h.StartDate, EndDate = h.EndDate, ReasonForLeaving = hc.Reason }, f));
        }

        var uq = await CompanyEmployeeRules.CheckUniqueAsync(_db, v!, cid, 0, 0);
        if (uq != null) return Bad(uq);

        var saved = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var code = v!.Code ?? await CompanyEmployeeRules.NextCodeAsync(_db, cid);

            var user = new hrms.Models.User
            {
                Name = v.Name,
                Email = v.Email,
                PhoneNumber = v.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password!),
                IsActive = true,
                MainRoleId = null,
                RoleId = null
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var emp = new Employee
            {
                UserId = user.Id,
                CompanyId = cid,
                CompanyRoleId = req.CompanyRoleId,
                EmployeeCode = code,
                OfficialEmail = v.Official,
                EmergencyNumber = v.Emergency,
                DateOfBirth = v.Dob,
                Gender = v.Gender,
                BloodGroup = v.Blood,
                MaritalStatus = v.Marital,
                Nationality = v.Nationality,
                AadharNumber = v.Aadhar,
                PanNumber = v.Pan,
                Address = v.Address,
                HighestQualification = v.Qualification,
                Institution = v.Institution,
                YearOfPassing = v.Year,
                WorkType = v.WorkType,
                IsActive = true
            };

            if (profile != null) emp.ProfilePicture = await CompanyEmployeeRules.SaveFileAsync(_env, profile, cid, "profile", saved);

            foreach (var (row, f) in docs)
            {
                if (f != null) row.FilePath = await CompanyEmployeeRules.SaveFileAsync(_env, f, cid, "documents", saved);
                emp.Documents.Add(row);
            }

            foreach (var (row, f) in hist)
            {
                if (f != null) row.RelievingLetter = await CompanyEmployeeRules.SaveFileAsync(_env, f, cid, "history", saved);
                emp.Histories.Add(row);
            }

            foreach (var mid in mgrIds)
                _db.ReportingManagers.Add(new ReportingManager { UserId = user.Id, ManagerUserId = mid });

            _db.Employees.Add(emp);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { id = emp.Id, userId = user.Id, employeeCode = emp.EmployeeCode });
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            CompanyEmployeeRules.CleanupNew(saved);
            return Conflict(new { message = "Some details already exist. Check email, phone, Aadhar, PAN or employee ID." });
        }
        catch
        {
            await tx.RollbackAsync();
            CompanyEmployeeRules.CleanupNew(saved);
            throw;
        }
    }
}