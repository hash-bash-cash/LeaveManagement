using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Constants;
using LMS.Data;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

public class DebugController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DebugController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("setup-admin")]
    public async Task<IActionResult> SetupAdmin()
    {
        var adminEmail = "admin@lms.com";
        var user = await _userManager.FindByEmailAsync(adminEmail);
        
        if (user != null)
        {
            user.IsActive = true;
            user.Status = UserStatus.Active;
            
            // Just force reset the password to be absolutely sure
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, "Admin@123");
            
            await _userManager.UpdateAsync(user);
            return Ok("Admin user updated and password reset to Admin@123");
        }
        
        return NotFound("Admin user not found. Seeder failed entirely.");
    }
}
