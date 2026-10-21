using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("holidays")]
[Index(nameof(CompanyId), Name = "ix_holidays_company")]
public class Holiday
{
    [Column("id")] public int Id { get; set; }

    // null = global
    [Column("company_id")] public int? CompanyId { get; set; }
    [ForeignKey("CompanyId")] public Company? Company { get; set; }

    [Column("name")] public string Name { get; set; } = string.Empty;

    // date | weekly | nth
    [Column("kind")] public string Kind { get; set; } = "date";

    [Column("holiday_date")] public DateOnly? Date { get; set; }
    [Column("yearly")] public bool Yearly { get; set; }

    // 0 = Sunday ... 6 = Saturday
    [Column("weekday")] public int? Weekday { get; set; }

    // "2,4" = 2nd and 4th occurrence in month
    [Column("nth")] public string? Nth { get; set; }

    [Column("color")] public string Color { get; set; } = "#ef4444";
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}