using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        var mainrole = await context.MainRoles
            .FirstOrDefaultAsync(r => r.Slug == "super-admin");

        if (mainrole == null)
        {
            mainrole = new MainRole
            {
                Name = "Super Admin",
                Slug = "super-admin",
                IsActive = true
            };

            context.MainRoles.Add(mainrole);
            await context.SaveChangesAsync();
        }

        var admin = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "hrms-admin2k6@gmail.com");

        if (admin == null)
        {
            context.Users.Add(new User
            {
                Name = "Hrms Admin",
                Email = "hrms-admin2k6@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("HrmsAdmin@2026"),
                MainRoleId = mainrole.Id,   // changed from MainRole = mainrole.Id
                IsActive = true
            });

            await context.SaveChangesAsync();
        }
    }
}