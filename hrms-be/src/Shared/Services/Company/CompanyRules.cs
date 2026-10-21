using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public class CompanyForm
{
    public string? CompanyName { get; set; }
    public string? OwnerName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? About { get; set; }
    public string? Theme { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public IFormFile? Logo { get; set; }
}

public static class CompanyRules
{
    private static readonly Regex PhoneRx = new(@"^\+?\d{7,15}$");
    private static readonly string[] LogoExt = { ".png", ".jpg", ".jpeg", ".webp" };

    public static string Name(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    public static string Email(string? v) => (v ?? string.Empty).Trim().ToLowerInvariant();
    public static string Phone(string? v) => new((v ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
    public static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static string WebRoot(IWebHostEnvironment env) =>
        env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public static Dictionary<string, string> Validate(CompanyForm f, bool isCreate)
    {
        var e = new Dictionary<string, string>();

        var name = Name(f.CompanyName);
        if (name.Length == 0) e["companyName"] = "Company name is required.";
        else if (name.Length > 150) e["companyName"] = "Max 150 characters.";

        if (Name(f.OwnerName).Length == 0) e["ownerName"] = "Owner name is required.";

        var email = Email(f.Email);
        if (email.Length == 0) e["email"] = "Email is required.";
        else if (!new EmailAddressAttribute().IsValid(email)) e["email"] = "Enter a valid email.";

        if (!PhoneRx.IsMatch(Phone(f.PhoneNumber))) e["phoneNumber"] = "Enter a valid phone number.";

        var wa = Phone(f.WhatsappNumber);
        if (wa.Length > 0 && !PhoneRx.IsMatch(wa)) e["whatsappNumber"] = "Enter a valid WhatsApp number.";

        var half = (f.Latitude == null) != (f.Longitude == null);
        var range = (f.Latitude != null && (f.Latitude < -90 || f.Latitude > 90))
                 || (f.Longitude != null && (f.Longitude < -180 || f.Longitude > 180));
        if (half || range) e["location"] = "Invalid location.";

        if (f.Logo != null)
        {
            var ext = Path.GetExtension(f.Logo.FileName).ToLowerInvariant();
            if (!LogoExt.Contains(ext)) e["logo"] = "Use PNG, JPG or WebP.";
            else if (f.Logo.Length > 2 * 1024 * 1024) e["logo"] = "Max size is 2 MB.";
        }

        if (!string.IsNullOrWhiteSpace(f.Theme))
        {
            try
            {
                if (f.Theme.Length > 3000) throw new JsonException();
                JsonDocument.Parse(f.Theme).Dispose();
            }
            catch (JsonException) { e["theme"] = "Invalid theme."; }
        }

        if (isCreate)
        {
            if ((f.Password ?? string.Empty).Length < 6) e["password"] = "Min 6 characters.";
            if (f.Password != f.ConfirmPassword) e["confirmPassword"] = "Passwords don't match.";
        }

        return e;
    }

    // only checks the fields that are filled in (so the inline check can send one field)
    public static async Task<Dictionary<string, string>> DuplicatesAsync(AppDbContext db, CompanyForm f, int? excludeCompanyId)
    {
        var e = new Dictionary<string, string>();
        var exCompany = excludeCompanyId ?? 0;
        var exUser = exCompany == 0
            ? 0
            : await db.Companies.Where(c => c.Id == exCompany).Select(c => c.UserId).FirstOrDefaultAsync();

        var name = Name(f.CompanyName).ToLowerInvariant();
        if (name.Length > 0 && await db.Companies.AnyAsync(c => c.Name.ToLower() == name && c.Id != exCompany))
            e["companyName"] = "Company name already exists.";

        var email = Email(f.Email);
        if (email.Length > 0 && await db.Users.AnyAsync(u => u.Email == email && u.Id != exUser))
            e["email"] = "Email already registered.";

        var phone = Phone(f.PhoneNumber);
        if (phone.Length > 0 && await db.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != exUser))
            e["phoneNumber"] = "Phone number already registered.";

        var wa = Phone(f.WhatsappNumber);
        if (wa.Length > 0 && await db.Companies.AnyAsync(c => c.WhatsappNumber == wa && c.Id != exCompany))
            e["whatsappNumber"] = "WhatsApp number already used.";

        return e;
    }

    public static async Task<string> SaveLogoAsync(IFormFile file, string webRoot)
    {
        var dir = Path.Combine(webRoot, "uploads", "companies");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var fs = File.Create(Path.Combine(dir, fileName));
        await file.CopyToAsync(fs);
        return $"/uploads/companies/{fileName}";
    }

    public static void DeleteLogo(string? path, string webRoot)
    {
        if (string.IsNullOrEmpty(path)) return;
        var full = Path.Combine(webRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }
}