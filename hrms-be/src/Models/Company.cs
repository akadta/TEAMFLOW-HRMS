using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("companies")]
[Index(nameof(UserId), IsUnique = true, Name = "ux_companies_user_id")]
[Index(nameof(Name), IsUnique = true, Name = "ux_companies_name")]
[Index(nameof(WhatsappNumber), IsUnique = true, Name = "ux_companies_whatsapp")]
public class Company
{
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("owner_name")]
    public string OwnerName { get; set; } = string.Empty;

    [Column("logo")]
    public string? Logo { get; set; }

    [Column("theme")]
    public string Theme { get; set; } = "{}";

    [Column("address")]
    public string? Address { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("about")]
    public string? About { get; set; }

    [Column("whatsapp_number")]
    public string? WhatsappNumber { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}