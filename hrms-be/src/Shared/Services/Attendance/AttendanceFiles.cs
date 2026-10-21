using hrms.Models;

namespace hrms.Shared.Services;

public static class AttendanceFiles
{
    private static readonly string[] Ext = { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".doc", ".docx" };

    public static string? Check(IReadOnlyCollection<IFormFile> files)
    {
        if (files.Count > 5) return "Max 5 files.";
        foreach (var f in files)
        {
            if (!Ext.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) return "File type not allowed.";
            if (f.Length > 8_000_000) return "Each file: max 8 MB.";
        }
        return null;
    }

    public static async Task<AttendanceRequestFile> SaveAsync(
        IFormFile f, IWebHostEnvironment env, int companyId, List<string> saved)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var rel = $"uploads/attendance/{companyId}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full))
        {
            await f.CopyToAsync(fs);
        }

        saved.Add(full);
        var name = Path.GetFileName(f.FileName);
        return new AttendanceRequestFile
        {
            FileName = name.Length > 150 ? name[..150] : name,
            FilePath = "/" + rel
        };
    }
    public static async Task<(string Name, string Path)> SaveRawAsync(
    IFormFile f, IWebHostEnvironment env, int companyId, List<string> saved)
    {
        var r = await SaveAsync(f, env, companyId, saved);
        return (r.FileName, r.FilePath);
    }

    public static void Cleanup(List<string> saved)
    {
        foreach (var p in saved)
        {
            try { System.IO.File.Delete(p); } catch { }
        }
    }
}