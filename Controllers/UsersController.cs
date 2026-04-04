using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Constants;
using LMS.Data;
using LMS.Services;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly LMS.Services.ILeaveAllocationService _leaveAllocationService;
    private readonly IEmailService _emailService;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, LMS.Services.ILeaveAllocationService leaveAllocationService, IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _leaveAllocationService = leaveAllocationService;
        _emailService = emailService;
    }

    // GET: Users
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var userViewModels = new List<UserViewModel>();

        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            userViewModels.Add(new UserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Role = userRoles.FirstOrDefault() ?? "None"
            });
        }

        return View(userViewModels);
    }

    // GET: Users/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        if (id == null) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var userRoles = await _userManager.GetRolesAsync(user);
        var model = new UserViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            Role = userRoles.FirstOrDefault() ?? "None"
        };
        
        var availableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        ViewBag.Roles = availableRoles;

        return View(model);
    }

    // POST: Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserViewModel model)
    {
        if (id != model.Id) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (ModelState.IsValid)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            return RedirectToAction(nameof(Index));
        }

        var availableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        ViewBag.Roles = availableRoles;
        return View(model);
    }

    // GET: Users/Pending
    public async Task<IActionResult> Pending()
    {
        var users = await _userManager.Users.Where(u => u.Status == UserStatus.Pending).ToListAsync();
        var userViewModels = new List<UserViewModel>();

        foreach (var user in users)
        {
            userViewModels.Add(new UserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                EmployeeCode = user.EmployeeCode,
                Phone = user.Phone,
                Shift = user.Shift,
                Status = user.Status.ToString()
            });
        }

        return View(userViewModels);
    }

    // GET: Users/Review/5
    public async Task<IActionResult> Review(string id)
    {
        if (id == null) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.Status != UserStatus.Pending) return NotFound();

        var model = new UserViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            EmployeeCode = user.EmployeeCode,
            Phone = user.Phone,
            Shift = user.Shift,
            Role = Roles.Employee // Default recommendation
        };

        await PopulateReviewViewBags();
        return View(model);
    }

    // POST: Users/Review/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string id, UserViewModel model)
    {
        if (id != model.Id) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (ModelState.IsValid)
        {
            // Update User Organizational Details
            user.DepartmentId = model.DepartmentId;
            user.ManagerId = model.ManagerId;
            
            // Activate User
            user.IsActive = true;
            user.Status = UserStatus.Active;

            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded)
            {
                // Assign Role
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);

                // ** Auto-allocate default leaves for the newly activated user **
                await _leaveAllocationService.AllocateDefaultLeavesAsync(user.Id, DateTime.Now.Year);

                // Notify User
                string subject = "Your Account Has Been Approved!";
                string body = $@"
                    <h3>Welcome to the Leave Management System</h3>
                    <p>Hello {user.FirstName},</p>
                    <p>Your account has been approved by the administrator.</p>
                    <p>You can now log in using your registered email and password.</p>
                ";
                if(user.Email != null)
                {
                    await _emailService.SendEmailAsync(user.Email, subject, body);
                }

                return RedirectToAction(nameof(Pending));
            }
            
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        await PopulateReviewViewBags();
        return View(model);
    }

    private async Task PopulateReviewViewBags()
    {
        ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        ViewBag.Departments = await _context.Departments.ToListAsync();
        
        // Find possible managers (users with Manager role)
        var managers = await _userManager.GetUsersInRoleAsync(Roles.Manager);
        ViewBag.Managers = managers.Where(m => m.IsActive).ToList();
    }

    // POST: Users/Reject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id)
    {
        if (id == null) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.Status != UserStatus.Pending) return NotFound();

        user.Status = UserStatus.Rejected;
        user.IsActive = false;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Notify User
            string subject = "Account Registration Update";
            string body = $@"
                <p>Hello {user.FirstName},</p>
                <p>We regret to inform you that your account registration request for the Leave Management System has been rejected.</p>
                <p>Please contact your administrator for more details.</p>
            ";
            if (user.Email != null)
            {
                await _emailService.SendEmailAsync(user.Email, subject, body);
            }
            
            TempData["Success"] = "User request rejected successfully.";
        }
        else
        {
            TempData["Error"] = "Error rejecting user request.";
        }

        return RedirectToAction(nameof(Pending));
    }
}
