// using cic.Data;
// using cic.Models;
// using Microsoft.EntityFrameworkCore;

// namespace cic.Seeders;

// public static class RoleSeeder
// {
//     public static async Task SeedAsync(AppDbContext context)
//     {
//         var executiveRole = await context.Roles
//             .FirstOrDefaultAsync(r => r.Slug == "executive");

//         if (executiveRole == null)
//         {
//             context.Roles.Add(new Role
//             {
//                 Name = "Executive",
//                 Slug = "executive",
//                 IsActive = true
//             });

//             await context.SaveChangesAsync();
//         }
//     }
// }