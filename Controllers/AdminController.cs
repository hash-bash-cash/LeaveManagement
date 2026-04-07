using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Data;
using LMS.Models;
using LMS.Constants;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ─── PROFILE ────────────────────────────────────────────────────────────
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        
        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        
        return View(user);
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

        var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction(nameof(Profile));
    }

    // ─── DASHBOARD ──────────────────────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;
        var employees = await _userManager.GetUsersInRoleAsync(Roles.Employee);
        var managers  = await _userManager.GetUsersInRoleAsync(Roles.Manager);

        var onLeaveToday = await _context.LeaveRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.LeaveType)
            .Where(r => r.Approved == true && !r.Cancelled
                     && r.StartDate <= today && r.EndDate >= today)
            .ToListAsync();

        var upcomingHolidays = await _context.Holidays
            .Where(h => h.Date >= today)
            .OrderBy(h => h.Date)
            .Take(5)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalEmployees        = employees.Count,
            TotalManagers         = managers.Count,
            ActiveLeaveRequests   = await _context.LeaveRequests.CountAsync(r => !r.Cancelled && r.Approved == true),
            PendingApprovals      = await _context.LeaveRequests.CountAsync(r => r.Approved == null && !r.Cancelled),
            TotalDepartments      = await _context.Departments.CountAsync(d => d.ParentDepartmentId == null),
            TotalSubDepartments   = await _context.Departments.CountAsync(d => d.ParentDepartmentId != null),
            EmployeesOnLeaveToday = onLeaveToday.Count,
            UpcomingHolidays      = upcomingHolidays.Count,
            EmployeesOnLeaveTodayList = onLeaveToday,
            UpcomingHolidayList       = upcomingHolidays
        };

        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    // ─── ALL LEAVE REQUESTS ─────────────────────────────────────────────────
    public async Task<IActionResult> AllLeaveRequests()
    {
        var leaves = await GetMappedLeaves(null);
        return View(leaves);
    }

    // ─── PENDING APPROVALS ──────────────────────────────────────────────────
    public async Task<IActionResult> PendingApprovals()
    {
        var leaves = await GetMappedLeaves("Pending");
        return View(leaves);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? remarks)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = true;
        leave.ReviewerId = admin.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeId);
        if (leaveType != null)
        {
            if (leaveType.Code == "LW")
            {
                var fortyFiveDaysAgo = DateTime.Today.AddDays(-45);
                var recentLwps = await _context.LeaveRequests
                    .Where(r => r.RequestingEmployeeId == leave.RequestingEmployeeId && r.Approved == true && !r.Cancelled && r.LeaveTypeId == leave.LeaveTypeId && r.StartDate >= fortyFiveDaysAgo)
                    .ToListAsync();
                
                int totalLwpDays = 0;
                foreach(var l in recentLwps) 
                {
                    for (var d = l.StartDate; d <= l.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) totalLwpDays++;
                }
                
                int currentLwpDays = 0;
                for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) currentLwpDays++;

                if (totalLwpDays + currentLwpDays >= 15 && totalLwpDays < 15)
                {
                    var plType = await _context.LeaveTypes.FirstOrDefaultAsync(l => l.Code == "PL");
                    if (plType != null)
                    {
                        var plAlloc = await _context.LeaveAllocations.FirstOrDefaultAsync(a => a.EmployeeId == leave.RequestingEmployeeId && a.LeaveTypeId == plType.Id && a.Period == DateTime.Now.Year);
                        if (plAlloc != null && plAlloc.NumberOfDays > 0)
                        {
                            plAlloc.NumberOfDays -= 1;
                            _context.LeaveAllocations.Update(plAlloc);
                        }
                    }
                }
            }
            else if (leaveType.Code == "CO")
            {
                int businessDays = 0;
                for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) businessDays++;
                businessDays -= await _context.Holidays.CountAsync(h => h.Date >= leave.StartDate && h.Date <= leave.EndDate);
                
                var unusedCOs = await _context.CompensatoryOffs.Where(c => c.EmployeeId == leave.RequestingEmployeeId && !c.IsUsed && c.ExpiryDate >= leave.EndDate).OrderBy(c => c.ExpiryDate).Take(businessDays).ToListAsync();
                foreach(var co in unusedCOs)
                {
                    co.IsUsed = true;
                    _context.CompensatoryOffs.Update(co);
                }
            }
        }

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Approved (Admin)",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Approved by Admin.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request approved successfully.";
        return RedirectToAction(nameof(LeaveHistory));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? remarks)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = false;
        leave.ReviewerId = admin.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Rejected (Admin)",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Rejected by Admin.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request rejected.";
        return RedirectToAction(nameof(LeaveHistory));
    }

    // ─── LEAVE HISTORY (CONSOLIDATED) ───────────────────────────────────────
    public async Task<IActionResult> LeaveHistory()
    {
        var today = DateTime.Today;

        // Today's Leaves
        var todayLeaves = await GetMappedLeaves("Today");
        
        // Pending Leaves
        var pendingLeaves = await GetMappedLeaves("Pending");

        // Department-wise Stats
        var departments = await _context.Departments
            .Include(d => d.SubDepartments)
            .ToListAsync();

        var deptStats = await _context.Departments
            .Select(dept => new DepartmentLeaveStatsViewModel
            {
                DepartmentName = dept.Name,
                TotalEmployees = _context.Users.Count(u => u.DepartmentId == dept.Id),
                OnLeaveToday = _context.LeaveRequests.Count(r => 
                    r.RequestingEmployee!.DepartmentId == dept.Id 
                    && r.Approved == true && !r.Cancelled 
                    && r.StartDate <= today && r.EndDate >= today),
                PendingApprovals = _context.LeaveRequests.Count(r => 
                    r.RequestingEmployee!.DepartmentId == dept.Id 
                    && r.Approved == null && !r.Cancelled)
            })
            .OrderBy(d => d.DepartmentName)
            .ToListAsync();

        // Employee Stats
        var allUsers = await _userManager.Users.ToListAsync();
        var empStats = new List<EmployeeLeaveStatsViewModel>();
        foreach (var user in allUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "No Role";

            var totalLeaves = await _context.LeaveRequests
                .Where(r => r.RequestingEmployeeId == user.Id && r.Approved == true && !r.Cancelled)
                .CountAsync();

            var pending = await _context.LeaveRequests
                .Where(r => r.RequestingEmployeeId == user.Id && r.Approved == null && !r.Cancelled)
                .CountAsync();

            empStats.Add(new EmployeeLeaveStatsViewModel
            {
                EmployeeName = $"{user.FirstName} {user.LastName}",
                Role = role,
                TotalLeavesTaken = totalLeaves,
                PendingRequests = pending
            });
        }

        var model = new AdminLeaveHistoryViewModel
        {
            OnLeaveTodayCount = todayLeaves.Count,
            PendingApprovalsCount = pendingLeaves.Count,
            TotalEmployees = allUsers.Count,
            TotalAbsent = todayLeaves.Count,
            TodayLeaves = todayLeaves,
            PendingLeaves = pendingLeaves,
            DepartmentStats = deptStats,
            EmployeeStats = empStats,
            LeaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync()
        };

        return View(model);
    }

    // ─── EMPLOYEE LEAVE HISTORY ─────────────────────────────────────────────
    public async Task<IActionResult> EmployeeLeaveHistory(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var employee = await _userManager.FindByIdAsync(id);
        if (employee == null) return NotFound();

        var leaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Reviewer)
            .Where(r => r.RequestingEmployeeId == id)
            .OrderByDescending(r => r.DateRequested)
            .ToListAsync();

        ViewBag.Employee = employee;
        return View(leaves);
    }



    // ─── LEAVE STRUCTURE ────────────────────────────────────────────────────
    public async Task<IActionResult> LeaveStructure()
    {
        var leaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync();
        return View(leaveTypes);
    }

    [HttpGet]
    public async Task<IActionResult> EditLeaveType(int id)
    {
        var leaveType = await _context.LeaveTypes.FindAsync(id);
        if (leaveType == null) return NotFound();
        return View(leaveType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLeaveType(int id, LeaveType model)
    {
        if (id != model.Id) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = await _context.LeaveTypes.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name               = model.Name;
            existing.Code               = model.Code;
            existing.DefaultDays        = model.DefaultDays;
            existing.IsPaid             = model.IsPaid;
            existing.RequiresApproval   = model.RequiresApproval;
            existing.MaxConsecutiveDays = model.MaxConsecutiveDays;
            existing.CarryForward       = model.CarryForward;
            existing.YearlyLimit        = model.YearlyLimit;
            existing.IsEnabled          = model.IsEnabled;
            existing.DateModified       = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Leave type rules updated successfully.";
            return RedirectToAction(nameof(LeaveStructure));
        }
        return View(model);
    }

    // ─── HELPER: Map LeaveRequests to ViewModels ─────────────────────────────
    private async Task<List<AdminLeaveRequestViewModel>> GetMappedLeaves(string? filter)
    {
        var query = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Include(r => r.Reviewer)
            .AsQueryable();

        query = filter switch
        {
            "Pending"  => query.Where(r => r.Approved == null && !r.Cancelled),
            "Approved" => query.Where(r => r.Approved == true && !r.Cancelled),
            "Rejected" => query.Where(r => r.Approved == false && !r.Cancelled),
            "Today"    => query.Where(r => r.Approved == true && !r.Cancelled && r.StartDate <= DateTime.Today && r.EndDate >= DateTime.Today),
            _          => query
        };

        var leaves = await query.OrderByDescending(r => r.DateRequested).ToListAsync();

        return leaves.Select(r => new AdminLeaveRequestViewModel
        {
            Id             = r.Id,
            EmployeeId     = r.RequestingEmployeeId,
            EmployeeName   = $"{r.RequestingEmployee?.FirstName} {r.RequestingEmployee?.LastName}",
            EmployeeEmail  = r.RequestingEmployee?.Email ?? "",
            LeaveTypeName  = r.LeaveType?.Name ?? "",
            StartDate      = r.StartDate,
            EndDate        = r.EndDate,
            TotalDays      = (int)(r.EndDate - r.StartDate).TotalDays + 1,
            Reason         = r.RequestComments,
            DateRequested  = r.DateRequested,
            Status         = r.Cancelled ? "Cancelled" : r.Approved == null ? "Pending" : r.Approved == true ? "Approved" : "Rejected",
            ManagerRemarks = r.ManagerRemarks,
            ReviewerName   = r.Reviewer != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : null,
            DateActioned   = r.DateActioned,
            AttachmentPath = r.AttachmentPath,
            Cancelled      = r.Cancelled
        }).ToList();
    }
}
