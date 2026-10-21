using System.Text.Json;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record EmployeeEditDocumentInput(int? Id, string? Type, string? Title, string? Description, string? FileKey, bool RemoveFile);
public record EmployeeEditHistoryInput(int? Id, string? Employer, DateOnly? StartDate, DateOnly? EndDate, string? ReasonForLeaving, string? FileKey, bool RemoveFile);
public record EmployeeEditRequest(
        int CompanyId,
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
    List<EmployeeEditDocumentInput>? Documents,
    List<EmployeeEditHistoryInput>? Histories,
    List<int>? ManagerUserIds);

[ApiController]
[Authorize]
[Route("api/main/employee-edit")]
public class EmployeeEditController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public EmployeeEditController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Regex EmailRx = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRx = new(@"^\d{10}$", RegexOptions.Compiled);
    private static readonly Regex AadharRx = new(@"^\d{12}$", RegexOptions.Compiled);
    private static readonly Regex PanRx = new(@"^[A-Z]{5}\d{4}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex CodeRx = new(@"^[A-Z0-9][A-Z0-9\-_/]{1,29}$", RegexOptions.Compiled);

    private static readonly string[] Genders = { "Male", "Female", "Other" };
    private static readonly string[] BloodGroups = { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };
    private static readonly string[] Maritals = { "Single", "Married", "Divorced", "Widowed" };
    private static readonly string[] WorkTypes = { "office", "remote", "hybrid" };
    private static readonly string[] DocTypes = { "Qualification Certificate", "Technical Certification", "Achievement", "Research", "Other" };
    private static readonly string[] Reasons = { "Resignation", "Better Opportunity", "Contract Ended", "Terminated", "Relocation", "Higher Studies", "Other" };
    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] DocExt = { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".doc", ".docx" };

    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static string? Opt(string? v) { var c = Clean(v); return c.Length == 0 ? null : c; }
    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private async Task<string> SaveFile(IFormFile f, int companyId, string folder, List<string> saved)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var rel = $"uploads/employees/{companyId}/{folder}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(_env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full))
        {
            await f.CopyToAsync(fs);
        }

        saved.Add(full);
        return "/" + rel;
    }

    private static void CleanupNew(List<string> saved)
    {
        foreach (var p in saved)
        {
            try { System.IO.File.Delete(p); } catch { }
        }
    }

    private void DeleteOld(string? rel)
    {
        if (string.IsNullOrEmpty(rel)) return;
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(Path.Combine(root, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(full)) return;
        try { System.IO.File.Delete(full); } catch { }
    }

    // GET api/main/employee-edit/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var d = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.CompanyId,
                CompanyName = e.Company.Name,
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

    // PUT api/main/employee-edit/5
    // multipart: "payload" (json) + files: profilePicture, doc_0.., hist_0..
    [HttpPut("{id:int}")]
    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<IActionResult> Update(int id, [FromForm] string payload)
    {
        EmployeeEditRequest? req;
        try { req = JsonSerializer.Deserialize<EmployeeEditRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var emp = await _db.Employees
            .Include(e => e.User)
            .Include(e => e.Documents)
            .Include(e => e.Histories)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return NotFound(new { message = "Employee not found." });

        var userId = emp.UserId;

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CompanyId);
        if (company == null) return Bad("Company not found.");
        if (!company.IsActive && company.Id != emp.CompanyId) return Bad("Company is inactive.");

        var companyId = req.CompanyId; // target company (may differ from the current one)

        // ---------- basic ----------
        var name = Clean(req.Name);
        if (name.Length == 0) return Bad("Full name is required.");
        if (name.Length > 100) return Bad("Name: max 100 characters.");

        var email = Clean(req.Email).ToLowerInvariant();
        if (!EmailRx.IsMatch(email) || email.Length > 150) return Bad("Enter a valid email.");

        var phone = Clean(req.PhoneNumber);
        if (!PhoneRx.IsMatch(phone)) return Bad("Phone must be 10 digits.");

        var emergency = Opt(req.EmergencyNumber);
        if (emergency != null && !PhoneRx.IsMatch(emergency)) return Bad("Emergency number must be 10 digits.");

        var official = Opt(req.OfficialEmail)?.ToLowerInvariant();
        if (official != null && (!EmailRx.IsMatch(official) || official.Length > 150)) return Bad("Enter a valid official email.");
        if (official == email) return Bad("Official mail must differ from email.");

        var code = Opt(req.EmployeeCode)?.ToUpperInvariant();
        if (code == null) return Bad("Employee ID is required.");
        if (!CodeRx.IsMatch(code)) return Bad("Employee ID: 2-30 chars, letters, numbers, - _ /");

        // ---------- personal ----------
        var dob = req.DateOfBirth;
        if (dob != null && (dob > DateOnly.FromDateTime(DateTime.UtcNow) || dob < new DateOnly(1900, 1, 1)))
            return Bad("Date of birth is not valid.");

        var gender = Opt(req.Gender);
        if (gender != null && !Genders.Contains(gender)) return Bad("Invalid gender.");

        var blood = Opt(req.BloodGroup);
        if (blood != null && !BloodGroups.Contains(blood)) return Bad("Invalid blood group.");

        var marital = Opt(req.MaritalStatus);
        if (marital != null && !Maritals.Contains(marital)) return Bad("Invalid marital status.");

        var nationality = Opt(req.Nationality);
        if (nationality?.Length > 50) return Bad("Nationality: max 50 characters.");

        var aadhar = Opt(req.AadharNumber);
        if (aadhar != null && !AadharRx.IsMatch(aadhar)) return Bad("Aadhar must be 12 digits.");

        var pan = Opt(req.PanNumber)?.ToUpperInvariant();
        if (pan != null && !PanRx.IsMatch(pan)) return Bad("PAN format: AAAAA0000A.");

        var address = Opt(req.Address);
        if (address?.Length > 500) return Bad("Address: max 500 characters.");

        // ---------- professional ----------
        var qualification = Opt(req.HighestQualification);
        if (qualification?.Length > 100) return Bad("Qualification: max 100 characters.");

        var institution = Opt(req.Institution);
        if (institution?.Length > 150) return Bad("Institution: max 150 characters.");

        var year = req.YearOfPassing;
        if (year != null && (year < 1950 || year > DateTime.UtcNow.Year + 1)) return Bad("Year of passing is not valid.");

        // ---------- designation ----------
        var workType = Opt(req.WorkType)?.ToLowerInvariant();
        if (workType != null && !WorkTypes.Contains(workType)) return Bad("Invalid work type.");

        if (req.CompanyRoleId != null)
        {
            var role = await _db.CompanyRoles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == req.CompanyRoleId && r.CompanyId == companyId);
            if (role == null) return Bad("Role not found for this company.");
            if (!role.IsActive && role.Id != emp.CompanyRoleId) return Bad("Selected role is inactive.");
        }
        // ---------- reporting managers ----------
        var existingMgrIds = await _db.ReportingManagers.AsNoTracking()
            .Where(r => r.UserId == userId).Select(r => r.ManagerUserId).ToListAsync();

        // null = the client did not send it, so leave the stored managers alone
        var finalMgrIds = req.ManagerUserIds?.Distinct().ToList() ?? existingMgrIds;

        if (finalMgrIds.Count > ReportingRules.MaxManagers)
            return Bad($"Max {ReportingRules.MaxManagers} reporting managers.");

        var mgrErr = await ReportingRules.ValidateAsync(_db, userId, finalMgrIds.Except(existingMgrIds).ToList());
        if (mgrErr != null) return Bad(mgrErr);

        // the owner of the (new) company is always a manager too; make sure that is not a loop
        if (await ReportingRules.WouldLoopAsync(_db, userId, company.UserId))
            return Bad("The owner of that company already reports to this employee (directly or through others).");

        // ---------- password (optional reset) ----------
        string? newHash = null;
        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            var pw = req.NewPassword;
            if (pw.Length < 8 || pw.Length > 64) return Bad("Password must be 8-64 characters.");
            if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return Bad("Password needs a letter and a number.");
            if (pw != req.ConfirmPassword) return Bad("Passwords do not match.");
            newHash = BCrypt.Net.BCrypt.HashPassword(pw);
        }

        // ---------- files ----------
        var files = Request.Form.Files;

        var profile = files.GetFile("profilePicture");
        if (profile != null)
        {
            if (!ImageExt.Contains(Path.GetExtension(profile.FileName).ToLowerInvariant())) return Bad("Profile picture must be JPG, PNG or WEBP.");
            if (profile.Length > 3_000_000) return Bad("Profile picture: max 3 MB.");
        }

        // ---------- documents ----------
        var docsIn = req.Documents ?? new();
        if (docsIn.Count > 30) return Bad("Max 30 documents.");

        var docPlan = new List<(EmployeeDocument? Existing, EmployeeEditDocumentInput In, string Type, string Title, string? Desc, IFormFile? File)>();
        var keepDocIds = new HashSet<int>();
        for (var i = 0; i < docsIn.Count; i++)
        {
            var d = docsIn[i];
            var title = Clean(d.Title);
            if (title.Length == 0) return Bad($"Document {i + 1}: title is required.");
            if (title.Length > 150) return Bad($"Document {i + 1}: title max 150 characters.");

            var type = Opt(d.Type) ?? "Other";
            if (!DocTypes.Contains(type)) return Bad($"Document {i + 1}: invalid type.");

            var desc = Opt(d.Description);
            if (desc?.Length > 500) return Bad($"Document {i + 1}: description max 500 characters.");

            EmployeeDocument? existing = null;
            if (d.Id != null)
            {
                existing = emp.Documents.FirstOrDefault(x => x.Id == d.Id);
                if (existing == null) return Bad($"Document {i + 1}: not found.");
                if (!keepDocIds.Add(existing.Id)) return Bad($"Document {i + 1}: duplicated.");
            }

            var f = d.FileKey == null ? null : files.GetFile(d.FileKey);
            if (f != null)
            {
                if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return Bad($"Document {i + 1}: file type not allowed.");
                if (f.Length > 8_000_000) return Bad($"Document {i + 1}: file max 8 MB.");
            }

            docPlan.Add((existing, d, type, title, desc, f));
        }

        // ---------- history ----------
        var histIn = req.Histories ?? new();
        if (histIn.Count > 20) return Bad("Max 20 employment entries.");

        var histPlan = new List<(EmployeeHistory? Existing, EmployeeEditHistoryInput In, string Employer, string? Reason, IFormFile? File)>();
        var keepHistIds = new HashSet<int>();
        for (var i = 0; i < histIn.Count; i++)
        {
            var h = histIn[i];
            var employer = Clean(h.Employer);
            if (employer.Length == 0) return Bad($"Employment {i + 1}: employer is required.");
            if (employer.Length > 150) return Bad($"Employment {i + 1}: employer max 150 characters.");
            if (h.StartDate != null && h.EndDate != null && h.EndDate < h.StartDate) return Bad($"Employment {i + 1}: end date is before start date.");

            var reason = Opt(h.ReasonForLeaving);
            if (reason != null && !Reasons.Contains(reason)) return Bad($"Employment {i + 1}: invalid reason.");

            EmployeeHistory? existing = null;
            if (h.Id != null)
            {
                existing = emp.Histories.FirstOrDefault(x => x.Id == h.Id);
                if (existing == null) return Bad($"Employment {i + 1}: not found.");
                if (!keepHistIds.Add(existing.Id)) return Bad($"Employment {i + 1}: duplicated.");
            }

            var f = h.FileKey == null ? null : files.GetFile(h.FileKey);
            if (f != null)
            {
                if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return Bad($"Employment {i + 1}: file type not allowed.");
                if (f.Length > 8_000_000) return Bad($"Employment {i + 1}: file max 8 MB.");
            }

            histPlan.Add((existing, h, employer, reason, f));
        }

        // ---------- uniqueness (excluding this employee) ----------
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == email)) return Bad("This email is already registered.");
        if (await _db.Employees.AnyAsync(e => e.Id != id && e.OfficialEmail == email)) return Bad("This email is already used as an official mail.");
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.PhoneNumber == phone)) return Bad("This phone number is already registered.");

        if (official != null)
        {
            if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == official)) return Bad("This official mail is already registered.");
            if (await _db.Employees.AnyAsync(e => e.Id != id && e.OfficialEmail == official)) return Bad("This official mail is already used.");
        }

        if (aadhar != null && await _db.Employees.AnyAsync(e => e.Id != id && e.AadharNumber == aadhar)) return Bad("This Aadhar number already exists.");
        if (pan != null && await _db.Employees.AnyAsync(e => e.Id != id && e.PanNumber == pan)) return Bad("This PAN number already exists.");
        if (await _db.Employees.AnyAsync(e => e.Id != id && e.CompanyId == companyId && e.EmployeeCode == code))
            return Bad("This employee ID already exists in the company.");

        // ---------- apply ----------
        var newFiles = new List<string>();     // delete if save fails
        var oldFiles = new List<string?>();    // delete after save succeeds

        try
        {
            emp.User.Name = name;
            emp.User.Email = email;
            emp.User.PhoneNumber = phone;
            if (newHash != null) emp.User.PasswordHash = newHash;
            emp.User.UpdatedAt = DateTime.UtcNow;

            emp.EmployeeCode = code;
            emp.CompanyId = companyId;
            emp.OfficialEmail = official;
            emp.EmergencyNumber = emergency;
            emp.DateOfBirth = dob;
            emp.Gender = gender;
            emp.BloodGroup = blood;
            emp.MaritalStatus = marital;
            emp.Nationality = nationality;
            emp.AadharNumber = aadhar;
            emp.PanNumber = pan;
            emp.Address = address;
            emp.HighestQualification = qualification;
            emp.Institution = institution;
            emp.YearOfPassing = year;
            emp.CompanyRoleId = req.CompanyRoleId;
            emp.WorkType = workType;
            emp.UpdatedAt = DateTime.UtcNow;

            if (profile != null)
            {
                oldFiles.Add(emp.ProfilePicture);
                emp.ProfilePicture = await SaveFile(profile, companyId, "profile", newFiles);
            }
            else if (req.RemoveProfilePicture && emp.ProfilePicture != null)
            {
                oldFiles.Add(emp.ProfilePicture);
                emp.ProfilePicture = null;
            }

            // documents: remove the ones that are no longer sent
            foreach (var gone in emp.Documents.Where(x => !keepDocIds.Contains(x.Id)).ToList())
            {
                oldFiles.Add(gone.FilePath);
                _db.Remove(gone);
            }

            foreach (var p in docPlan)
            {
                var row = p.Existing ?? new EmployeeDocument();
                row.Type = p.Type;
                row.Title = p.Title;
                row.Description = p.Desc;

                if (p.File != null)
                {
                    oldFiles.Add(row.FilePath);
                    row.FilePath = await SaveFile(p.File, companyId, "documents", newFiles);
                }
                else if (p.In.RemoveFile && row.FilePath != null)
                {
                    oldFiles.Add(row.FilePath);
                    row.FilePath = null;
                }

                if (p.Existing == null) emp.Documents.Add(row);
            }

            // history
            foreach (var gone in emp.Histories.Where(x => !keepHistIds.Contains(x.Id)).ToList())
            {
                oldFiles.Add(gone.RelievingLetter);
                _db.Remove(gone);
            }

            foreach (var p in histPlan)
            {
                var row = p.Existing ?? new EmployeeHistory();
                row.Employer = p.Employer;
                row.StartDate = p.In.StartDate;
                row.EndDate = p.In.EndDate;
                row.ReasonForLeaving = p.Reason;

                if (p.File != null)
                {
                    oldFiles.Add(row.RelievingLetter);
                    row.RelievingLetter = await SaveFile(p.File, companyId, "history", newFiles);
                }
                else if (p.In.RemoveFile && row.RelievingLetter != null)
                {
                    oldFiles.Add(row.RelievingLetter);
                    row.RelievingLetter = null;
                }

                if (p.Existing == null) emp.Histories.Add(row);
            }

            if (req.ManagerUserIds != null)
                await ReportingRules.SyncAsync(_db, userId, finalMgrIds);

            await _db.SaveChangesAsync(); // single call = single transaction
        }
        catch (DbUpdateException)
        {
            CleanupNew(newFiles);
            return Conflict(new { message = "Some details already exist. Check email, phone, Aadhar, PAN or employee ID." });
        }
        catch
        {
            CleanupNew(newFiles);
            throw;
        }

        foreach (var p in oldFiles) DeleteOld(p);

        return Ok(new { id = emp.Id, employeeCode = emp.EmployeeCode });
    }
}