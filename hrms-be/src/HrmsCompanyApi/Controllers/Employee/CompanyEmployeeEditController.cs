using System.Text.Json;
using hrms.Data;
using hrms.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hrms.Shared.Authorization;

namespace hrms.HrmsCompanyApi.Controllers;

public record CompanyEmployeeEditDocInput(int? Id, string? Type, string? Title, string? Description, string? FileKey, bool RemoveFile);
public record CompanyEmployeeEditHistInput(int? Id, string? Employer, DateOnly? StartDate, DateOnly? EndDate, string? ReasonForLeaving, string? FileKey, bool RemoveFile);
public record CompanyEmployeeEditRequest(
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
    string? NewPassword,
    string? ConfirmPassword,
    bool RemoveProfilePicture,
    List<CompanyEmployeeEditDocInput>? Documents,
    List<CompanyEmployeeEditHistInput>? Histories,
    List<int>? ManagerUserIds);

[Route("api/company/employee-edit")]
public class CompanyEmployeeEditController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CompanyEmployeeEditController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // private IActionResult? OwnerOnly() =>
    //     IsCompanyOwner ? null : StatusCode(403, new { message = "Only the company owner can do this." });

    // GET api/company/employee-edit/5
    [HttpGet("{id:int}")]
    [RequirePermission("employees.edit")]
    public async Task<IActionResult> Get(int id)
    {
        var cid = CurrentCompanyId;
        var d = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == cid)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.EmployeeCode,
                Name = e.User.Name,
                Email = e.User.Email,
                PhoneNumber = e.User.PhoneNumber,
                e.EmergencyNumber,
                e.OfficialEmail,
                e.DateOfBirth,
                e.Gender,
                e.BloodGroup,
                e.MaritalStatus,
                e.Nationality,
                e.AadharNumber,
                e.PanNumber,
                e.Address,
                e.ProfilePicture,
                e.HighestQualification,
                e.Institution,
                e.YearOfPassing,
                CompanyRoleId = e.CompanyRoleId,
                e.WorkType,
                e.IsActive,
                Documents = e.Documents.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.Type, x.Title, x.Description, x.FilePath }),
                Histories = e.Histories.OrderByDescending(x => x.StartDate)
                    .Select(x => new { x.Id, x.Employer, x.StartDate, x.EndDate, x.ReasonForLeaving, x.RelievingLetter })
            })
            .FirstOrDefaultAsync();

        if (d == null) return NotFound(new { message = "Employee not found." });
        return Ok(d);
    }

    // PUT api/company/employee-edit/5
    // multipart: "payload" (json) + files: profilePicture, doc_0.., hist_0..
    [HttpPut("{id:int}")]
    [RequirePermission("employees.edit")]
    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<IActionResult> Update(int id, [FromForm] string payload)
    {
        // var deny = OwnerOnly();
        // if (deny != null) return deny;

        CompanyEmployeeEditRequest? req;
        try { req = JsonSerializer.Deserialize<CompanyEmployeeEditRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var cid = CurrentCompanyId;
        var emp = await _db.Employees
            .Include(e => e.User)
            .Include(e => e.Documents)
            .Include(e => e.Histories)
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == cid);
        if (emp == null) return NotFound(new { message = "Employee not found." });

        var userId = emp.UserId;

        var err = CompanyEmployeeRules.CheckCore(new EmpCore(
            req.EmployeeCode, req.Name, req.Email, req.PhoneNumber, req.EmergencyNumber, req.OfficialEmail,
            req.DateOfBirth, req.Gender, req.BloodGroup, req.MaritalStatus, req.Nationality, req.AadharNumber,
            req.PanNumber, req.Address, req.HighestQualification, req.Institution, req.YearOfPassing, req.WorkType),
            true, out var v);
        if (err != null) return Bad(err);

        if (req.CompanyRoleId != null)
        {
            var role = await _db.CompanyRoles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == req.CompanyRoleId && r.CompanyId == cid);
            if (role == null) return Bad("Role not found for this company.");
            if (!role.IsActive && role.Id != emp.CompanyRoleId) return Bad("Selected role is inactive.");
        }

        // ---------- reporting managers (this company's employees only) ----------
        var inCompanyExisting = await _db.ReportingManagers.AsNoTracking()
            .Where(r => r.UserId == userId &&
                        _db.Employees.Any(e => e.UserId == r.ManagerUserId && e.CompanyId == cid))
            .Select(r => r.ManagerUserId)
            .ToListAsync();

        // null = the page did not send it, so leave the managers alone
        var finalMgrIds = req.ManagerUserIds?.Distinct().ToList() ?? inCompanyExisting;
        if (finalMgrIds.Count > CompanyReportingRules.MaxManagers)
            return Bad($"Max {CompanyReportingRules.MaxManagers} reporting managers.");

        var mgrErr = await CompanyReportingRules.ValidateAsync(
            _db, userId, finalMgrIds.Except(inCompanyExisting).ToList(), cid);
        if (mgrErr != null) return Bad(mgrErr);

        string? newHash = null;
        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            var pwErr = CompanyEmployeeRules.CheckPassword(req.NewPassword, req.ConfirmPassword);
            if (pwErr != null) return Bad(pwErr);
            newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        }

        var files = Request.Form.Files;
        var profile = files.GetFile("profilePicture");
        var pErr = CompanyEmployeeRules.CheckProfile(profile);
        if (pErr != null) return Bad(pErr);

        // ---------- documents ----------
        var docsIn = req.Documents ?? new();
        if (docsIn.Count > 30) return Bad("Max 30 documents.");

        var docPlan = new List<(EmployeeDocument? Existing, CompanyEmployeeEditDocInput In, DocClean C, IFormFile? File)>();
        var keepDocIds = new HashSet<int>();
        for (var i = 0; i < docsIn.Count; i++)
        {
            var d = docsIn[i];
            var f = d.FileKey == null ? null : files.GetFile(d.FileKey);
            var e = CompanyEmployeeRules.CheckDoc(i, d.Title, d.Type, d.Description, f, out var dc);
            if (e != null) return Bad(e);

            EmployeeDocument? existing = null;
            if (d.Id != null)
            {
                existing = emp.Documents.FirstOrDefault(x => x.Id == d.Id);
                if (existing == null) return Bad($"Document {i + 1}: not found.");
                if (!keepDocIds.Add(existing.Id)) return Bad($"Document {i + 1}: duplicated.");
            }

            docPlan.Add((existing, d, dc!, f));
        }

        // ---------- history ----------
        var histIn = req.Histories ?? new();
        if (histIn.Count > 20) return Bad("Max 20 employment entries.");

        var histPlan = new List<(EmployeeHistory? Existing, CompanyEmployeeEditHistInput In, HistClean C, IFormFile? File)>();
        var keepHistIds = new HashSet<int>();
        for (var i = 0; i < histIn.Count; i++)
        {
            var h = histIn[i];
            var f = h.FileKey == null ? null : files.GetFile(h.FileKey);
            var e = CompanyEmployeeRules.CheckHist(i, h.Employer, h.StartDate, h.EndDate, h.ReasonForLeaving, f, out var hc);
            if (e != null) return Bad(e);

            EmployeeHistory? existing = null;
            if (h.Id != null)
            {
                existing = emp.Histories.FirstOrDefault(x => x.Id == h.Id);
                if (existing == null) return Bad($"Employment {i + 1}: not found.");
                if (!keepHistIds.Add(existing.Id)) return Bad($"Employment {i + 1}: duplicated.");
            }

            histPlan.Add((existing, h, hc!, f));
        }

        var uq = await CompanyEmployeeRules.CheckUniqueAsync(_db, v!, cid, id, userId);
        if (uq != null) return Bad(uq);

        // ---------- apply ----------
        var newFiles = new List<string>();     // delete if save fails
        var oldFiles = new List<string?>();    // delete after save succeeds

        try
        {
            emp.User.Name = v!.Name;
            emp.User.Email = v.Email;
            emp.User.PhoneNumber = v.Phone;
            if (newHash != null) emp.User.PasswordHash = newHash;
            emp.User.UpdatedAt = DateTime.UtcNow;

            emp.EmployeeCode = v.Code!;
            emp.OfficialEmail = v.Official;
            emp.EmergencyNumber = v.Emergency;
            emp.DateOfBirth = v.Dob;
            emp.Gender = v.Gender;
            emp.BloodGroup = v.Blood;
            emp.MaritalStatus = v.Marital;
            emp.Nationality = v.Nationality;
            emp.AadharNumber = v.Aadhar;
            emp.PanNumber = v.Pan;
            emp.Address = v.Address;
            emp.HighestQualification = v.Qualification;
            emp.Institution = v.Institution;
            emp.YearOfPassing = v.Year;
            emp.CompanyRoleId = req.CompanyRoleId;
            emp.WorkType = v.WorkType;
            emp.UpdatedAt = DateTime.UtcNow;

            if (profile != null)
            {
                oldFiles.Add(emp.ProfilePicture);
                emp.ProfilePicture = await CompanyEmployeeRules.SaveFileAsync(_env, profile, cid, "profile", newFiles);
            }
            else if (req.RemoveProfilePicture && emp.ProfilePicture != null)
            {
                oldFiles.Add(emp.ProfilePicture);
                emp.ProfilePicture = null;
            }

            foreach (var gone in emp.Documents.Where(x => !keepDocIds.Contains(x.Id)).ToList())
            {
                oldFiles.Add(gone.FilePath);
                _db.Remove(gone);
            }

            foreach (var p in docPlan)
            {
                var row = p.Existing ?? new EmployeeDocument();
                row.Type = p.C.Type;
                row.Title = p.C.Title;
                row.Description = p.C.Desc;

                if (p.File != null)
                {
                    oldFiles.Add(row.FilePath);
                    row.FilePath = await CompanyEmployeeRules.SaveFileAsync(_env, p.File, cid, "documents", newFiles);
                }
                else if (p.In.RemoveFile && row.FilePath != null)
                {
                    oldFiles.Add(row.FilePath);
                    row.FilePath = null;
                }

                if (p.Existing == null) emp.Documents.Add(row);
            }

            foreach (var gone in emp.Histories.Where(x => !keepHistIds.Contains(x.Id)).ToList())
            {
                oldFiles.Add(gone.RelievingLetter);
                _db.Remove(gone);
            }

            foreach (var p in histPlan)
            {
                var row = p.Existing ?? new EmployeeHistory();
                row.Employer = p.C.Employer;
                row.StartDate = p.In.StartDate;
                row.EndDate = p.In.EndDate;
                row.ReasonForLeaving = p.C.Reason;

                if (p.File != null)
                {
                    oldFiles.Add(row.RelievingLetter);
                    row.RelievingLetter = await CompanyEmployeeRules.SaveFileAsync(_env, p.File, cid, "history", newFiles);
                }
                else if (p.In.RemoveFile && row.RelievingLetter != null)
                {
                    oldFiles.Add(row.RelievingLetter);
                    row.RelievingLetter = null;
                }

                if (p.Existing == null) emp.Histories.Add(row);
            }

            if (req.ManagerUserIds != null)
            {
                // only this company's managers are changed; links set elsewhere stay
                var drop = await _db.ReportingManagers
                    .Where(r => r.UserId == userId &&
                                inCompanyExisting.Contains(r.ManagerUserId) &&
                                !finalMgrIds.Contains(r.ManagerUserId))
                    .ToListAsync();
                _db.ReportingManagers.RemoveRange(drop);

                foreach (var mid in finalMgrIds.Except(inCompanyExisting))
                    _db.ReportingManagers.Add(new ReportingManager { UserId = userId, ManagerUserId = mid });
            }

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            CompanyEmployeeRules.CleanupNew(newFiles);
            return Conflict(new { message = "Some details already exist. Check email, phone, Aadhar, PAN or employee ID." });
        }
        catch
        {
            CompanyEmployeeRules.CleanupNew(newFiles);
            throw;
        }

        foreach (var p in oldFiles) CompanyEmployeeRules.DeleteOld(_env, p);

        return Ok(new { id = emp.Id, employeeCode = emp.EmployeeCode });
    }
}