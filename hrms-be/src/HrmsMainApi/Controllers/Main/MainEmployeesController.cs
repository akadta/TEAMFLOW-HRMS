using System.Text.Json;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hrms.Shared.Authorization;
namespace hrms.HrmsMainApi.Controllers;

public record MainEmployeeSaveRequest(
    int? RoleId, string? EmployeeCode, string? Name, string? Email, string? PhoneNumber, string? EmergencyNumber,
    string? OfficialEmail, DateOnly? DateOfBirth, string? Gender, string? BloodGroup, string? MaritalStatus,
    string? Nationality, string? AadharNumber, string? PanNumber, string? Address, string? HighestQualification,
    string? Institution, int? YearOfPassing, string? WorkType, string? Password, string? ConfirmPassword,
    bool RemoveProfilePicture, List<int>? ManagerUserIds);

public record MainEmployeeStatusRequest(bool IsActive);

[ApiController]
[Authorize]
[Route("api/main/main-employees")]
public class MainEmployeesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MainEmployeesController(AppDbContext db, IWebHostEnvironment env)
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
    private static readonly Regex CodeRx = new(@"^MAIN-(\d+)[A-Z]?$", RegexOptions.Compiled);

    private static readonly string[] Genders = { "Male", "Female", "Other" };
    private static readonly string[] BloodGroups = { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };
    private static readonly string[] Maritals = { "Single", "Married", "Divorced", "Widowed" };
    private static readonly string[] WorkTypes = { "office", "remote", "hybrid" };
    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };

    private static string Cln(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static string? Opt(string? v) { var c = Cln(v); return c.Length == 0 ? null : c; }
    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private sealed record Vals(
        int RoleId, string? Code, string Name, string Email, string Phone, string? Emergency, string? Official,
        DateOnly? Dob, string? Gender, string? Blood, string? Marital, string? Nationality, string? Aadhar,
        string? Pan, string? Address, string? Qualification, string? Institution, int? Year, string? WorkType);

    private async Task<(string? Error, Vals? Data)> CheckAsync(MainEmployeeSaveRequest r, int? currentRoleId)
    {
        var name = Cln(r.Name);
        if (name.Length == 0) return ("Full name is required.", null);
        if (name.Length > 100) return ("Name: max 100 characters.", null);

        var email = Cln(r.Email).ToLowerInvariant();
        if (!EmailRx.IsMatch(email) || email.Length > 150) return ("Enter a valid email.", null);

        var phone = Cln(r.PhoneNumber);
        if (!PhoneRx.IsMatch(phone)) return ("Phone must be 10 digits.", null);

        var emergency = Opt(r.EmergencyNumber);
        if (emergency != null && !PhoneRx.IsMatch(emergency)) return ("Emergency number must be 10 digits.", null);

        var official = Opt(r.OfficialEmail)?.ToLowerInvariant();
        if (official != null && (!EmailRx.IsMatch(official) || official.Length > 150)) return ("Enter a valid official email.", null);
        if (official == email) return ("Official mail must differ from email.", null);

        var code = Opt(r.EmployeeCode)?.ToUpperInvariant();
        if (code != null && !ManualRx.IsMatch(code)) return ("Employee ID: 2-30 chars, letters, numbers, - _ /", null);

        var dob = r.DateOfBirth;
        if (dob != null && (dob > DateOnly.FromDateTime(DateTime.UtcNow) || dob < new DateOnly(1900, 1, 1)))
            return ("Date of birth is not valid.", null);

        var gender = Opt(r.Gender);
        if (gender != null && !Genders.Contains(gender)) return ("Invalid gender.", null);
        var blood = Opt(r.BloodGroup);
        if (blood != null && !BloodGroups.Contains(blood)) return ("Invalid blood group.", null);
        var marital = Opt(r.MaritalStatus);
        if (marital != null && !Maritals.Contains(marital)) return ("Invalid marital status.", null);
        var nationality = Opt(r.Nationality);
        if (nationality?.Length > 50) return ("Nationality: max 50 characters.", null);

        var aadhar = Opt(r.AadharNumber);
        if (aadhar != null && !AadharRx.IsMatch(aadhar)) return ("Aadhar must be 12 digits.", null);
        var pan = Opt(r.PanNumber)?.ToUpperInvariant();
        if (pan != null && !PanRx.IsMatch(pan)) return ("PAN format: AAAAA0000A.", null);

        var address = Opt(r.Address);
        if (address?.Length > 500) return ("Address: max 500 characters.", null);
        var qualification = Opt(r.HighestQualification);
        if (qualification?.Length > 100) return ("Qualification: max 100 characters.", null);
        var institution = Opt(r.Institution);
        if (institution?.Length > 150) return ("Institution: max 150 characters.", null);

        var year = r.YearOfPassing;
        if (year != null && (year < 1950 || year > DateTime.UtcNow.Year + 1)) return ("Year of passing is not valid.", null);

        var workType = Opt(r.WorkType)?.ToLowerInvariant();
        if (workType != null && !WorkTypes.Contains(workType)) return ("Invalid work type.", null);

        if (r.RoleId == null) return ("Select a role.", null);
        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.RoleId);
        if (role == null) return ("Role not found.", null);
        if (!role.IsActive && role.Id != currentRoleId) return ("Selected role is inactive.", null);

        return (null, new Vals(role.Id, code, name, email, phone, emergency, official, dob, gender, blood, marital,
            nationality, aadhar, pan, address, qualification, institution, year, workType));
    }

    private async Task<string?> UniqueAsync(Vals v, int userId, int selfId)
    {
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == v.Email)) return "This email is already registered.";
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.PhoneNumber == v.Phone)) return "This phone number is already registered.";

        if (await _db.Employees.AnyAsync(e => e.OfficialEmail == v.Email) ||
            await _db.MainEmployees.AnyAsync(m => m.Id != selfId && m.OfficialEmail == v.Email))
            return "This email is already used as an official mail.";

        if (v.Official != null)
        {
            if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == v.Official)) return "This official mail is already registered.";
            if (await _db.Employees.AnyAsync(e => e.OfficialEmail == v.Official) ||
                await _db.MainEmployees.AnyAsync(m => m.Id != selfId && m.OfficialEmail == v.Official))
                return "This official mail is already used.";
        }

        if (v.Aadhar != null && (await _db.Employees.AnyAsync(e => e.AadharNumber == v.Aadhar) ||
            await _db.MainEmployees.AnyAsync(m => m.Id != selfId && m.AadharNumber == v.Aadhar)))
            return "This Aadhar number already exists.";

        if (v.Pan != null && (await _db.Employees.AnyAsync(e => e.PanNumber == v.Pan) ||
            await _db.MainEmployees.AnyAsync(m => m.Id != selfId && m.PanNumber == v.Pan)))
            return "This PAN number already exists.";

        if (v.Code != null && await _db.MainEmployees.AnyAsync(m => m.Id != selfId && m.EmployeeCode == v.Code))
            return "This employee ID already exists.";

        return null;
    }

    private static string? CheckPassword(string? pw, string? confirm)
    {
        pw ??= string.Empty;
        if (pw.Length < 8 || pw.Length > 64) return "Password must be 8-64 characters.";
        if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return "Password needs a letter and a number.";
        if (pw != confirm) return "Passwords do not match.";
        return null;
    }

    private static string? CheckProfile(IFormFile? f)
    {
        if (f == null) return null;
        if (!ImageExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return "Profile picture must be JPG, PNG or WEBP.";
        if (f.Length > 3_000_000) return "Profile picture: max 3 MB.";
        return null;
    }

    // MAIN-001A, MAIN-002K ...
    private async Task<string> NextCode()
    {
        var codes = await _db.MainEmployees.AsNoTracking().Select(m => m.EmployeeCode).ToListAsync();

        var max = 0;
        foreach (var c in codes)
        {
            var m = CodeRx.Match(c);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > max) max = n;
        }

        var taken = codes.ToHashSet();
        var seq = (max + 1).ToString("D3");
        string code;
        do { code = $"MAIN-{seq}{(char)('A' + Random.Shared.Next(26))}"; } while (taken.Contains(code));
        return code;
    }

    private async Task<string> SaveFile(IFormFile f, List<string> saved)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var rel = $"uploads/main-employees/profile/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(_env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full)) { await f.CopyToAsync(fs); }

        saved.Add(full);
        return "/" + rel;
    }

    private static void Cleanup(List<string> saved)
    {
        foreach (var p in saved) { try { System.IO.File.Delete(p); } catch { } }
    }

    private void DeleteOld(string? rel)
    {
        if (string.IsNullOrEmpty(rel)) return;
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(Path.Combine(root, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(full)) return;
        try { System.IO.File.Delete(full); } catch { }
    }

    // ---------- reads ----------

    [HttpGet("next-code")]
    [RequireMainPermission("main-employees.register")]
    public async Task<IActionResult> NextCodeFor() => Ok(new { code = await NextCode() });

    // GET api/main/main-employees?search=&status=active|inactive&page=1&pageSize=50
    [HttpGet]
    [RequireMainPermission("main-employees.list")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var q = _db.MainEmployees.AsNoTracking().AsQueryable();
        if (status == "active") q = q.Where(m => m.IsActive);
        else if (status == "inactive") q = q.Where(m => !m.IsActive);

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0)
            q = q.Where(m =>
                m.User.Name.ToLower().Contains(s) || m.User.Email.ToLower().Contains(s) ||
                (m.User.PhoneNumber ?? "").Contains(s) || m.EmployeeCode.ToLower().Contains(s) ||
                m.Role.Name.ToLower().Contains(s));

        var total = await q.CountAsync();
        var items = await q.OrderBy(m => m.User.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.EmployeeCode,
                Name = m.User.Name,
                Email = m.User.Email,
                Phone = m.User.PhoneNumber,
                Role = m.Role.Name,
                m.WorkType,
                m.ProfilePicture,
                m.IsActive
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    [RequireMainPermission("main-employees.list", "main-employees.edit")]
    public async Task<IActionResult> Get(int id)
    {
        var d = await _db.MainEmployees.AsNoTracking().Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.RoleId,
                Role = m.Role.Name,
                m.EmployeeCode,
                Name = m.User.Name,
                Email = m.User.Email,
                PhoneNumber = m.User.PhoneNumber,
                m.EmergencyNumber,
                m.OfficialEmail,
                m.DateOfBirth,
                m.Gender,
                m.BloodGroup,
                m.MaritalStatus,
                m.Nationality,
                m.AadharNumber,
                m.PanNumber,
                m.Address,
                m.ProfilePicture,
                m.HighestQualification,
                m.Institution,
                m.YearOfPassing,
                m.WorkType,
                m.IsActive
            })
            .FirstOrDefaultAsync();

        return d == null ? NotFound(new { message = "Main employee not found." }) : Ok(d);
    }

    // ---------- writes ----------

    // multipart: "payload" (json) + file: profilePicture
    [HttpPost]
    [RequireMainPermission("main-employees.register")]
    public async Task<IActionResult> Create([FromForm] string payload)
    {
        MainEmployeeSaveRequest? req;
        try { req = JsonSerializer.Deserialize<MainEmployeeSaveRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var (err, v) = await CheckAsync(req, null);
        if (err != null) return Bad(err);

        var pwErr = CheckPassword(req.Password, req.ConfirmPassword);
        if (pwErr != null) return Bad(pwErr);

        var profile = Request.Form.Files.GetFile("profilePicture");
        var pErr = CheckProfile(profile);
        if (pErr != null) return Bad(pErr);

        var mgrIds = (req.ManagerUserIds ?? new()).Distinct().ToList();
        var mErr = await ReportingRules.ValidateAsync(_db, null, mgrIds, personIsMain: true);
        if (mErr != null) return Bad(mErr);

        var uq = await UniqueAsync(v!, 0, 0);
        if (uq != null) return Bad(uq);

        var saved = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var code = v!.Code ?? await NextCode();

            var user = new hrms.Models.User
            {
                Name = v.Name,
                Email = v.Email,
                PhoneNumber = v.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password!),
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var emp = new MainEmployee
            {
                UserId = user.Id,
                RoleId = v.RoleId,
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
            if (profile != null) emp.ProfilePicture = await SaveFile(profile, saved);

            _db.MainEmployees.Add(emp);
            foreach (var mid in mgrIds)
                _db.ReportingManagers.Add(new ReportingManager { UserId = user.Id, ManagerUserId = mid });

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

    [HttpPut("{id:int}")]
    [RequireMainPermission("main-employees.edit")]
    public async Task<IActionResult> Update(int id, [FromForm] string payload)
    {
        MainEmployeeSaveRequest? req;
        try { req = JsonSerializer.Deserialize<MainEmployeeSaveRequest>(payload, Json); }
        catch (JsonException) { return Bad("Invalid form data."); }
        if (req == null) return Bad("Invalid form data.");

        var emp = await _db.MainEmployees.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == id);
        if (emp == null) return NotFound(new { message = "Main employee not found." });

        var (err, v) = await CheckAsync(req, emp.RoleId);
        if (err != null) return Bad(err);
        if (v!.Code == null) return Bad("Employee ID is required.");

        string? newHash = null;
        if (!string.IsNullOrEmpty(req.Password))
        {
            var pwErr = CheckPassword(req.Password, req.ConfirmPassword);
            if (pwErr != null) return Bad(pwErr);
            newHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }

        var profile = Request.Form.Files.GetFile("profilePicture");
        var pErr = CheckProfile(profile);
        if (pErr != null) return Bad(pErr);

        var userId = emp.UserId;
        var existing = await _db.ReportingManagers.AsNoTracking()
            .Where(r => r.UserId == userId).Select(r => r.ManagerUserId).ToListAsync();

        // null = the page did not send it, so leave the managers alone
        var finalMgr = req.ManagerUserIds?.Distinct().ToList() ?? existing;
        var mErr = await ReportingRules.ValidateAsync(_db, userId, finalMgr.Except(existing).ToList(), personIsMain: true);
        if (mErr != null) return Bad(mErr);

        var uq = await UniqueAsync(v, userId, id);
        if (uq != null) return Bad(uq);

        string? oldPicture = null;
        var saved = new List<string>();
        try
        {
            emp.User.Name = v.Name;
            emp.User.Email = v.Email;
            emp.User.PhoneNumber = v.Phone;
            if (newHash != null) emp.User.PasswordHash = newHash;
            emp.User.UpdatedAt = DateTime.UtcNow;

            emp.RoleId = v.RoleId;
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
            emp.WorkType = v.WorkType;
            emp.UpdatedAt = DateTime.UtcNow;

            if (profile != null)
            {
                oldPicture = emp.ProfilePicture;
                emp.ProfilePicture = await SaveFile(profile, saved);
            }
            else if (req.RemoveProfilePicture && emp.ProfilePicture != null)
            {
                oldPicture = emp.ProfilePicture;
                emp.ProfilePicture = null;
            }

            if (req.ManagerUserIds != null)
                await ReportingRules.SyncAsync(_db, userId, finalMgr);

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            Cleanup(saved);
            return Conflict(new { message = "Some details already exist. Check email, phone, Aadhar, PAN or employee ID." });
        }
        catch
        {
            Cleanup(saved);
            throw;
        }

        DeleteOld(oldPicture);
        return Ok(new { id = emp.Id, employeeCode = emp.EmployeeCode });
    }

    [HttpPatch("{id:int}/status")]
    [RequireMainPermission("main-employees.edit")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] MainEmployeeStatusRequest req)
    {
        var m = await _db.MainEmployees.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound(new { message = "Main employee not found." });
        if (m.UserId == CurrentUserId) return Bad("You cannot change your own status.");

        m.IsActive = req.IsActive;
        m.User.IsActive = req.IsActive;
        m.UpdatedAt = DateTime.UtcNow;
        m.User.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = m.IsActive });
    }

    [HttpDelete("{id:int}")]
    [RequireSuperAdmin]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await _db.MainEmployees.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound(new { message = "Main employee not found." });
        if (m.UserId == CurrentUserId) return Bad("You cannot delete yourself.");

        var picture = m.ProfilePicture;
        _db.MainEmployees.Remove(m);
        _db.Users.Remove(m.User);   // reporting links are removed by the cascade
        await _db.SaveChangesAsync();

        DeleteOld(picture);
        return Ok();
    }
}