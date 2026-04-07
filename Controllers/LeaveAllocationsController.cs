using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Data;
using LMS.Models;
using LMS.Constants;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class LeaveAllocationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : Controller
{
    // GET: LeaveAllocations
    public async Task<IActionResult> Index()
    {
        var leaveTypes = await context.LeaveTypes.ToListAsync();
        var model = new ManageLeaveAllocationViewModel
        {
            LeaveTypes = leaveTypes
        };
        return View(model);
    }

    // POST: LeaveAllocations/SetAllocations
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAllocations(int leaveTypeId, double numberOfDays)
    {
        var leaveType = await context.LeaveTypes.FindAsync(leaveTypeId);
        if (leaveType == null) return NotFound();

        var employees = await userManager.GetUsersInRoleAsync(Roles.Employee);
        var managers = await userManager.GetUsersInRoleAsync(Roles.Manager);
        var allUsers = employees.Concat(managers).ToList();

        var period = DateTime.Now.Year;
        int count = 0;

        foreach (var user in allUsers)
        {
            var exists = await context.LeaveAllocations
                .AnyAsync(q => q.EmployeeId == user.Id && q.LeaveTypeId == leaveTypeId && q.Period == period);

            if (!exists)
            {
                var allocation = new LeaveAllocation
                {
                    EmployeeId = user.Id,
                    LeaveTypeId = leaveTypeId,
                    NumberOfDays = numberOfDays,
                    Period = period,
                    DateCreated = DateTime.Now
                };
                context.Add(allocation);
                count++;
            }
        }

        if (count > 0)
        {
            await context.SaveChangesAsync();
            TempData["Success"] = $"Successfully allocated {numberOfDays} days of {leaveType.Name} to {count} users for {period}.";
        }
        else
        {
            TempData["Error"] = "No new allocations were created (users may already have allocations for this period).";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: LeaveAllocations/Details/5 (EmployeeId)
    public async Task<IActionResult> Details(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var allocations = await context.LeaveAllocations
            .Include(q => q.LeaveType)
            .Where(q => q.EmployeeId == id && q.Period == DateTime.Now.Year)
            .ToListAsync();

        var requests = await context.LeaveRequests
            .Include(q => q.LeaveType)
            .Where(q => q.RequestingEmployeeId == id)
            .ToListAsync();

        var model = new ViewAllocationsViewModel
        {
            Employee = user,
            Allocations = allocations
        };

        return View(model);
    }

    // POST: LeaveAllocations/EditAllocation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAllocation(int id, double numberOfDays)
    {
        var allocation = await context.LeaveAllocations.FindAsync(id);
        if (allocation == null) return NotFound();

        allocation.NumberOfDays = numberOfDays;
        allocation.DateModified = DateTime.Now;
        context.Update(allocation);
        await context.SaveChangesAsync();

        TempData["Success"] = "Leave allocation updated successfully.";
        return RedirectToAction(nameof(Details), new { id = allocation.EmployeeId });
    }
}

public class ManageLeaveAllocationViewModel
{
    public List<LeaveType> LeaveTypes { get; set; } = new();
}

public class ViewAllocationsViewModel
{
    public ApplicationUser Employee { get; set; } = null!;
    public List<LeaveAllocation> Allocations { get; set; } = new();
}
