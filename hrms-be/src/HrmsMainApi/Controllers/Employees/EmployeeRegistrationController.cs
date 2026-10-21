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

public record EmployeeDocumentInput(string? Type, string? Title, string? Description, string? FileKey);
public record EmployeeHistoryInput(string? Employer, DateOnly? StartDate, DateOnly? EndDate, string? ReasonForLeaving, string? FileKey);
public record EmployeeRegisterRequest(
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
    string? Password,
    string? ConfirmPassword,
      List<EmployeeDocumentInput>? Documents,
    List<EmployeeHistoryInput>? Histories,
    List<int>? ManagerUserIds);




[ApiController]
[Authorize]
[Route("api/main/employee-registration")]
public class EmployeeRegistrationController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public EmployeeRegistrationController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Regex EmailRx = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRx = new(@"^\d{10}$", RegexOptions.Compiled);
    private static readonly Regex AadharRx = new(@"^\d{12}$", RegexOptions.Compiled);
    private static readonly Regex PanRx = new(@"^[A-Z]{5}\d{4}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex ManualRx = new(@"^[A-Z0-9][A-Z0-9\-_/]{1,29}$", RegexOptions.Compiled);
    private static readonly Regex CodeRx = new(@"^EMP-(\d+)[A-Z]?$", RegexOptions.Compiled);

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

    // EMP-001A, EMP-002K ... width grows past 999 (EMP-1000B)
    private async Task<string> NextCode(int companyId)
    {
        var codes = await _db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.EmployeeCode)
            .ToListAsync();

        var max = 0;
        foreach (var c in codes)
        {
            var m = CodeRx.Match(c);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > max) max = n;
        }

        var taken = codes.ToHashSet();
        var seq = (max + 1).ToString("D3");
        string code;
        do
        {
            code = $"EMP-{seq}{(char)('A' + Random.Shared.Next(26))}";
        } while (taken.Contains(code));

        return code;
    }

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

    private static void Cleanup(List<string> saved)
    {
        foreach (var p in saved)
        {
            try { System.IO.File.Delete(p); } catch { }
        }
    }

    // GET api/main/employee-registration/next-code?companyId=1
    [HttpGet("next-code")]
    public async Task<IActionResult> NextCodeFor([FromQuery] int companyId)
    {
        if (!await _db.Companies.AnyAsync(c => c.Id == companyId))
            return NotFound(new { message = "Company not found." });

        return Ok(new { code = await NextCode(companyId) });
    }

    // multipart: "payload" (json) + files: profilePicture, doc_0.., hist_0..
    [HttpPost]
    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<IActionResult> Register([FromForm] string payload)
    {
        EmployeeRegisterRequest? req;
        try { req = JsonSerializer.Deserialize<EmployeeRegisterRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CompanyId);
        if (company == null) return NotFound(new { message = "Company not found." });
        if (!company.IsActive) return Bad("Company is inactive.");

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
        if (code != null && !ManualRx.IsMatch(code)) return Bad("Employee ID: 2-30 chars, letters, numbers, - _ /");

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
                .FirstOrDefaultAsync(r => r.Id == req.CompanyRoleId && r.CompanyId == req.CompanyId);
            if (role == null) return Bad("Role not found for this company.");
            if (!role.IsActive) return Bad("Selected role is inactive.");
        }
        // ---------- reporting managers ----------
        var mgrIds = (req.ManagerUserIds ?? new()).Distinct().ToList();
        var mgrErr = await ReportingRules.ValidateAsync(_db, null, mgrIds);
        if (mgrErr != null) return Bad(mgrErr);

        // ---------- password ----------
        var pw = req.Password ?? string.Empty;
        if (pw.Length < 8 || pw.Length > 64) return Bad("Password must be 8-64 characters.");
        if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return Bad("Password needs a letter and a number.");
        if (pw != req.ConfirmPassword) return Bad("Passwords do not match.");

        // ---------- files, documents, history ----------
        var files = Request.Form.Files;

        var profile = files.GetFile("profilePicture");
        if (profile != null)
        {
            if (!ImageExt.Contains(Path.GetExtension(profile.FileName).ToLowerInvariant())) return Bad("Profile picture must be JPG, PNG or WEBP.");
            if (profile.Length > 3_000_000) return Bad("Profile picture: max 3 MB.");
        }

        var docsIn = req.Documents ?? new();
        var histIn = req.Histories ?? new();
        if (docsIn.Count > 30) return Bad("Max 30 documents.");
        if (histIn.Count > 20) return Bad("Max 20 employment entries.");

        var docs = new List<(EmployeeDocument Row, IFormFile? File)>();
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

            var f = d.FileKey == null ? null : files.GetFile(d.FileKey);
            if (f != null)
            {
                if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return Bad($"Document {i + 1}: file type not allowed.");
                if (f.Length > 8_000_000) return Bad($"Document {i + 1}: file max 8 MB.");
            }

            docs.Add((new EmployeeDocument { Type = type, Title = title, Description = desc }, f));
        }

        var hist = new List<(EmployeeHistory Row, IFormFile? File)>();
        for (var i = 0; i < histIn.Count; i++)
        {
            var h = histIn[i];
            var employer = Clean(h.Employer);
            if (employer.Length == 0) return Bad($"Employment {i + 1}: employer is required.");
            if (employer.Length > 150) return Bad($"Employment {i + 1}: employer max 150 characters.");
            if (h.StartDate != null && h.EndDate != null && h.EndDate < h.StartDate) return Bad($"Employment {i + 1}: end date is before start date.");

            var reason = Opt(h.ReasonForLeaving);
            if (reason != null && !Reasons.Contains(reason)) return Bad($"Employment {i + 1}: invalid reason.");

            var f = h.FileKey == null ? null : files.GetFile(h.FileKey);
            if (f != null)
            {
                if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return Bad($"Employment {i + 1}: file type not allowed.");
                if (f.Length > 8_000_000) return Bad($"Employment {i + 1}: file max 8 MB.");
            }

            hist.Add((new EmployeeHistory { Employer = employer, StartDate = h.StartDate, EndDate = h.EndDate, ReasonForLeaving = reason }, f));
        }

        // ---------- uniqueness ----------
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email)) return Bad("This email is already registered.");
        if (await _db.Employees.AnyAsync(e => e.OfficialEmail == email)) return Bad("This email is already used as an official mail.");
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone)) return Bad("This phone number is already registered.");

        if (official != null)
        {
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == official)) return Bad("This official mail is already registered.");
            if (await _db.Employees.AnyAsync(e => e.OfficialEmail == official)) return Bad("This official mail is already used.");
        }

        if (aadhar != null && await _db.Employees.AnyAsync(e => e.AadharNumber == aadhar)) return Bad("This Aadhar number already exists.");
        if (pan != null && await _db.Employees.AnyAsync(e => e.PanNumber == pan)) return Bad("This PAN number already exists.");
        if (code != null && await _db.Employees.AnyAsync(e => e.CompanyId == req.CompanyId && e.EmployeeCode == code))
            return Bad("This employee ID already exists in the company.");

        // ---------- save (one transaction) ----------
        var saved = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            code ??= await NextCode(req.CompanyId);

            var user = new hrms.Models.User
            {
                Name = name,
                Email = email,
                PhoneNumber = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(pw),
                IsActive = true,
                MainRoleId = null,
                RoleId = null
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var emp = new Employee
            {
                UserId = user.Id,
                CompanyId = req.CompanyId,
                CompanyRoleId = req.CompanyRoleId,
                EmployeeCode = code,
                OfficialEmail = official,
                EmergencyNumber = emergency,
                DateOfBirth = dob,
                Gender = gender,
                BloodGroup = blood,
                MaritalStatus = marital,
                Nationality = nationality,
                AadharNumber = aadhar,
                PanNumber = pan,
                Address = address,
                HighestQualification = qualification,
                Institution = institution,
                YearOfPassing = year,
                WorkType = workType,
                IsActive = true
            };

            if (profile != null) emp.ProfilePicture = await SaveFile(profile, req.CompanyId, "profile", saved);

            foreach (var (row, f) in docs)
            {
                if (f != null) row.FilePath = await SaveFile(f, req.CompanyId, "documents", saved);
                emp.Documents.Add(row);
            }

            foreach (var (row, f) in hist)
            {
                if (f != null) row.RelievingLetter = await SaveFile(f, req.CompanyId, "history", saved);
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
            Cleanup(saved);
            return Conflict(new { message = "Some details already exist. Check email, phone, Aadhar, PAN or employee ID." });
        }
        catch
        {
            await tx.RollbackAsync();
            Cleanup(saved);
            throw;
        }
    }
}