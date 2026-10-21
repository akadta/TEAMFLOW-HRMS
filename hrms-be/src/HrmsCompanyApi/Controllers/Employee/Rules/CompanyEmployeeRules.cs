using System.Text.RegularExpressions;
using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record EmpCore(
    string? EmployeeCode, string? Name, string? Email, string? PhoneNumber, string? EmergencyNumber,
    string? OfficialEmail, DateOnly? DateOfBirth, string? Gender, string? BloodGroup, string? MaritalStatus,
    string? Nationality, string? AadharNumber, string? PanNumber, string? Address,
    string? HighestQualification, string? Institution, int? YearOfPassing, string? WorkType);

public record EmpClean(
    string Name, string Email, string Phone, string? Emergency, string? Official, string? Code,
    DateOnly? Dob, string? Gender, string? Blood, string? Marital, string? Nationality,
    string? Aadhar, string? Pan, string? Address, string? Qualification, string? Institution,
    int? Year, string? WorkType);

public record DocClean(string Type, string Title, string? Desc);
public record HistClean(string Employer, string? Reason);

public static class CompanyEmployeeRules
{
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

    public static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    public static string? Opt(string? v) { var c = Clean(v); return c.Length == 0 ? null : c; }

    // ---------- core fields ----------
    public static string? CheckCore(EmpCore c, bool codeRequired, out EmpClean? v)
    {
        v = null;

        var name = Clean(c.Name);
        if (name.Length == 0) return "Full name is required.";
        if (name.Length > 100) return "Name: max 100 characters.";

        var email = Clean(c.Email).ToLowerInvariant();
        if (!EmailRx.IsMatch(email) || email.Length > 150) return "Enter a valid email.";

        var phone = Clean(c.PhoneNumber);
        if (!PhoneRx.IsMatch(phone)) return "Phone must be 10 digits.";

        var emergency = Opt(c.EmergencyNumber);
        if (emergency != null && !PhoneRx.IsMatch(emergency)) return "Emergency number must be 10 digits.";

        var official = Opt(c.OfficialEmail)?.ToLowerInvariant();
        if (official != null && (!EmailRx.IsMatch(official) || official.Length > 150)) return "Enter a valid official email.";
        if (official == email) return "Official mail must differ from email.";

        var code = Opt(c.EmployeeCode)?.ToUpperInvariant();
        if (code == null && codeRequired) return "Employee ID is required.";
        if (code != null && !ManualRx.IsMatch(code)) return "Employee ID: 2-30 chars, letters, numbers, - _ /";

        var dob = c.DateOfBirth;
        if (dob != null && (dob > DateOnly.FromDateTime(DateTime.UtcNow) || dob < new DateOnly(1900, 1, 1)))
            return "Date of birth is not valid.";

        var gender = Opt(c.Gender);
        if (gender != null && !Genders.Contains(gender)) return "Invalid gender.";

        var blood = Opt(c.BloodGroup);
        if (blood != null && !BloodGroups.Contains(blood)) return "Invalid blood group.";

        var marital = Opt(c.MaritalStatus);
        if (marital != null && !Maritals.Contains(marital)) return "Invalid marital status.";

        var nationality = Opt(c.Nationality);
        if (nationality?.Length > 50) return "Nationality: max 50 characters.";

        var aadhar = Opt(c.AadharNumber);
        if (aadhar != null && !AadharRx.IsMatch(aadhar)) return "Aadhar must be 12 digits.";

        var pan = Opt(c.PanNumber)?.ToUpperInvariant();
        if (pan != null && !PanRx.IsMatch(pan)) return "PAN format: AAAAA0000A.";

        var address = Opt(c.Address);
        if (address?.Length > 500) return "Address: max 500 characters.";

        var qualification = Opt(c.HighestQualification);
        if (qualification?.Length > 100) return "Qualification: max 100 characters.";

        var institution = Opt(c.Institution);
        if (institution?.Length > 150) return "Institution: max 150 characters.";

        var year = c.YearOfPassing;
        if (year != null && (year < 1950 || year > DateTime.UtcNow.Year + 1)) return "Year of passing is not valid.";

        var workType = Opt(c.WorkType)?.ToLowerInvariant();
        if (workType != null && !WorkTypes.Contains(workType)) return "Invalid work type.";

        v = new EmpClean(name, email, phone, emergency, official, code, dob, gender, blood, marital,
            nationality, aadhar, pan, address, qualification, institution, year, workType);
        return null;
    }

    public static string? CheckPassword(string? pw, string? confirm)
    {
        pw ??= string.Empty;
        if (pw.Length < 8 || pw.Length > 64) return "Password must be 8-64 characters.";
        if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return "Password needs a letter and a number.";
        if (pw != confirm) return "Passwords do not match.";
        return null;
    }

    public static string? CheckProfile(IFormFile? f)
    {
        if (f == null) return null;
        if (!ImageExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return "Profile picture must be JPG, PNG or WEBP.";
        if (f.Length > 3_000_000) return "Profile picture: max 3 MB.";
        return null;
    }

    public static string? CheckDoc(int i, string? title, string? type, string? desc, IFormFile? f, out DocClean? r)
    {
        r = null;
        var t = Clean(title);
        if (t.Length == 0) return $"Document {i + 1}: title is required.";
        if (t.Length > 150) return $"Document {i + 1}: title max 150 characters.";

        var ty = Opt(type) ?? "Other";
        if (!DocTypes.Contains(ty)) return $"Document {i + 1}: invalid type.";

        var d = Opt(desc);
        if (d?.Length > 500) return $"Document {i + 1}: description max 500 characters.";

        if (f != null)
        {
            if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return $"Document {i + 1}: file type not allowed.";
            if (f.Length > 8_000_000) return $"Document {i + 1}: file max 8 MB.";
        }

        r = new DocClean(ty, t, d);
        return null;
    }

    public static string? CheckHist(int i, string? employer, DateOnly? start, DateOnly? end, string? reason, IFormFile? f, out HistClean? r)
    {
        r = null;
        var e = Clean(employer);
        if (e.Length == 0) return $"Employment {i + 1}: employer is required.";
        if (e.Length > 150) return $"Employment {i + 1}: employer max 150 characters.";
        if (start != null && end != null && end < start) return $"Employment {i + 1}: end date is before start date.";

        var rs = Opt(reason);
        if (rs != null && !Reasons.Contains(rs)) return $"Employment {i + 1}: invalid reason.";

        if (f != null)
        {
            if (!DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return $"Employment {i + 1}: file type not allowed.";
            if (f.Length > 8_000_000) return $"Employment {i + 1}: file max 8 MB.";
        }

        r = new HistClean(e, rs);
        return null;
    }

    // ---------- uniqueness (pass 0 / 0 when creating) ----------
    public static async Task<string?> CheckUniqueAsync(AppDbContext db, EmpClean v, int companyId, int exceptEmpId, int exceptUserId)
    {
        var email = v.Email;
        var phone = v.Phone;
        var official = v.Official;
        var aadhar = v.Aadhar;
        var pan = v.Pan;
        var code = v.Code;

        if (await db.Users.AnyAsync(u => u.Id != exceptUserId && u.Email.ToLower() == email)) return "This email is already registered.";
        if (await db.Employees.AnyAsync(e => e.Id != exceptEmpId && e.OfficialEmail == email)) return "This email is already used as an official mail.";
        if (await db.Users.AnyAsync(u => u.Id != exceptUserId && u.PhoneNumber == phone)) return "This phone number is already registered.";

        if (official != null)
        {
            if (await db.Users.AnyAsync(u => u.Id != exceptUserId && u.Email.ToLower() == official)) return "This official mail is already registered.";
            if (await db.Employees.AnyAsync(e => e.Id != exceptEmpId && e.OfficialEmail == official)) return "This official mail is already used.";
        }

        if (aadhar != null && await db.Employees.AnyAsync(e => e.Id != exceptEmpId && e.AadharNumber == aadhar)) return "This Aadhar number already exists.";
        if (pan != null && await db.Employees.AnyAsync(e => e.Id != exceptEmpId && e.PanNumber == pan)) return "This PAN number already exists.";
        if (code != null && await db.Employees.AnyAsync(e => e.Id != exceptEmpId && e.CompanyId == companyId && e.EmployeeCode == code))
            return "This employee ID already exists in the company.";

        return null;
    }

    // EMP-001A, EMP-002K ... width grows past 999
    public static async Task<string> NextCodeAsync(AppDbContext db, int companyId)
    {
        var codes = await db.Employees.AsNoTracking()
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

    // ---------- files ----------
    public static async Task<string> SaveFileAsync(IWebHostEnvironment env, IFormFile f, int companyId, string folder, List<string> saved)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var rel = $"uploads/employees/{companyId}/{folder}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full))
        {
            await f.CopyToAsync(fs);
        }

        saved.Add(full);
        return "/" + rel;
    }

    public static void CleanupNew(List<string> saved)
    {
        foreach (var p in saved)
        {
            try { System.IO.File.Delete(p); } catch { }
        }
    }

    public static void DeleteOld(IWebHostEnvironment env, string? rel)
    {
        if (string.IsNullOrEmpty(rel)) return;
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(Path.Combine(root, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(full)) return;
        try { System.IO.File.Delete(full); } catch { }
    }
}