using Microsoft.AspNetCore.Identity;
using LMS.Models;
using LMS.Constants;

namespace LMS.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { Roles.Admin, Roles.Manager, Roles.Employee };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@lms.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        
        if (adminUser == null)
        {
            var newAdmin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin",
                DateJoined = DateTime.UtcNow,
                IsActive = true,
                Status = UserStatus.Active
            };
            
            var result = await userManager.CreateAsync(newAdmin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, Roles.Admin);
            }
        }
        else
        {
            // Ensure existing admin is always active and has the Admin role
            bool needsUpdate = false;
            if (!adminUser.IsActive) { adminUser.IsActive = true; needsUpdate = true; }
            if (adminUser.Status != UserStatus.Active) { adminUser.Status = UserStatus.Active; needsUpdate = true; }
            if (needsUpdate) await userManager.UpdateAsync(adminUser);

            var adminRoles = await userManager.GetRolesAsync(adminUser);
            if (!adminRoles.Contains(Roles.Admin))
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }
    }
}
