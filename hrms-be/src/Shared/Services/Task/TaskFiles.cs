using hrms.Models;

namespace hrms.Shared.Services;

public static class TaskFiles
{
    private static readonly string[] Blocked =
        { ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".msi", ".vbs", ".js", ".html", ".htm", ".svg", ".php", ".aspx" };

    public static string? Check(IReadOnlyCollection<IFormFile> files, int existing = 0, int maxCount = 10, long maxBytes = 25_000_000)
    {
        if (existing + files.Count > maxCount) return $"Max {maxCount} files per task.";
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (Blocked.Contains(ext)) return $"{ext} files are not allowed.";
            if (f.Length > maxBytes) return $"Each file: max {maxBytes / 1_000_000} MB.";
        }
        return null;
    }
    public static async Task<WorkTaskFile> SaveAsync(IFormFile f, IWebHostEnvironment env, int companyId, List<string> saved)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var rel = $"uploads/tasks/{companyId}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full))
        {
            await f.CopyToAsync(fs);
        }

        saved.Add(full);
        var name = Path.GetFileName(f.FileName);
        return new WorkTaskFile
        {
            FileName = name.Length > 150 ? name[..150] : name,
            FilePath = "/" + rel,
            Size = f.Length
        };
    }
    public static async Task<(string Name, string Path, long Size)> SaveRawAsync(
        IFormFile f, IWebHostEnvironment env, int companyId, List<string> saved)
    {
        var r = await SaveAsync(f, env, companyId, saved);
        return (r.FileName, r.FilePath, r.Size);
    }
    public static void Delete(IWebHostEnvironment env, string? rel)
    {
        if (string.IsNullOrEmpty(rel)) return;
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(Path.Combine(root, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(full)) return;
        try { System.IO.File.Delete(full); } catch { }
    }
}