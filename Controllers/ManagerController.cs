using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Constants;
using LMS.Data;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Manager + "," + Roles.Admin)]
public class ManagerController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public ManagerController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // ─── DASHBOARD ──────────────────────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var today = DateTime.Today;
        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        var pendingRequests = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId) && r.Approved == null && !r.Cancelled)
            .OrderByDescending(r => r.DateRequested)
            .Take(5)
            .ToListAsync();

        var onLeaveToday = await _context.LeaveRequests
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId)
                     && r.Approved == true && !r.Cancelled
                     && r.StartDate <= today && r.EndDate >= today)
            .Select(r => r.RequestingEmployee!)
            .ToListAsync();

        var upcoming = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId)
                     && r.Approved == true && !r.Cancelled
                     && r.StartDate > today)
            .OrderBy(r => r.StartDate)
            .Take(5)
            .ToListAsync();

        var model = new ManagerDashboardViewModel
        {
            ManagerName = $"{manager.FirstName} {manager.LastName}",
            PendingRequests = await _context.LeaveRequests
                .CountAsync(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId) && r.Approved == null && !r.Cancelled),
            TeamMembersOnLeaveToday = onLeaveToday.Count,
            TotalTeamMembers = await myTeam.CountAsync(),
            UpcomingLeaveCount = await _context.LeaveRequests
                .CountAsync(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId) && r.Approved == true && r.StartDate > today),
            PendingLeaveRequests = pendingRequests,
            TeamOnLeaveToday = onLeaveToday,
            UpcomingLeaves = upcoming
        };

        return View(model);
    }

    // ─── PENDING REQUESTS ───────────────────────────────────────────────────
    public async Task<IActionResult> PendingRequests()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        var requests = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId) && r.Approved == null && !r.Cancelled)
            .OrderByDescending(r => r.DateRequested)
            .ToListAsync();

        return View(requests);
    }

    // ─── REVIEW (Approve/Reject) ────────────────────────────────────────────
    public async Task<IActionResult> Review(int id)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        var leave = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .FirstOrDefaultAsync(r => r.Id == id && myTeam.Any(u => u.Id == r.RequestingEmployeeId));

        if (leave == null) return NotFound();

        // Calculate balance
        var allocation = await _context.LeaveAllocations
            .FirstOrDefaultAsync(a => a.EmployeeId == leave.RequestingEmployeeId
                                   && a.LeaveTypeId == leave.LeaveTypeId
                                   && a.Period == DateTime.Now.Year);

        var usedRequests = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == leave.RequestingEmployeeId
                     && r.LeaveTypeId == leave.LeaveTypeId
                     && r.Approved == true && !r.Cancelled && r.Id != id)
            .ToListAsync();

        var usedDays = usedRequests.Sum(r => (int)(r.EndDate - r.StartDate).TotalDays + 1);

        var model = new LeaveApprovalViewModel
        {
            Id = leave.Id,
            EmployeeName = $"{leave.RequestingEmployee?.FirstName} {leave.RequestingEmployee?.LastName}",
            Email = leave.RequestingEmployee?.Email ?? "",
            LeaveTypeName = leave.LeaveType?.Name ?? "",
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            TotalDays = (int)(leave.EndDate - leave.StartDate).TotalDays + 1,
            Reason = leave.RequestComments,
            DateRequested = leave.DateRequested,
            AvailableBalance = (allocation?.NumberOfDays ?? 0) - usedDays,
            AttachmentPath = leave.AttachmentPath
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? remarks)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = true;
        leave.ReviewerId = manager.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Approved",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Approved.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request approved successfully.";
        return RedirectToAction(nameof(PendingRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? remarks)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = false;
        leave.ReviewerId = manager.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Rejected",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Rejected.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request rejected.";
        return RedirectToAction(nameof(PendingRequests));
    }

    // ─── LEAVE HISTORY ──────────────────────────────────────────────────────
    public async Task<IActionResult> History()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        var requests = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId))
            .OrderByDescending(r => r.DateRequested)
            .ToListAsync();

        return View(requests);
    }

    // ─── MY TEAM ────────────────────────────────────────────────────────────
    public async Task<IActionResult> MyTeam()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var teamMembers = await _userManager.Users
            .Include(u => u.Department)
            .Include(u => u.Team)
            .Where(u => u.ManagerId == manager.Id && u.IsActive)
            .ToListAsync();

        return View(teamMembers);
    }

    // ─── TEAM LEAVE BALANCE ─────────────────────────────────────────────────
    public async Task<IActionResult> TeamBalance()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var teamMembers = await _userManager.Users
            .Where(u => u.ManagerId == manager.Id && u.IsActive)
            .ToListAsync();

        var result = new List<TeamMemberBalanceViewModel>();

        foreach (var member in teamMembers)
        {
            var allocations = await _context.LeaveAllocations
                .Include(a => a.LeaveType)
                .Where(a => a.EmployeeId == member.Id && a.Period == DateTime.Now.Year)
                .ToListAsync();

            var usedLeaves = await _context.LeaveRequests
                .Where(r => r.RequestingEmployeeId == member.Id && r.Approved == true && !r.Cancelled)
                .ToListAsync();

            var balances = allocations.Select(a => new LeaveBalanceViewModel
            {
                LeaveTypeName = a.LeaveType!.Name,
                Allocated = a.NumberOfDays,
                Used = usedLeaves.Count(l => l.LeaveTypeId == a.LeaveTypeId)
            }).ToList();

            result.Add(new TeamMemberBalanceViewModel
            {
                EmployeeId = member.Id,
                EmployeeName = $"{member.FirstName} {member.LastName}",
                Email = member.Email ?? "",
                Shift = member.Shift,
                Balances = balances
            });
        }

        return View(result);
    }

    // ─── TEAM CALENDAR ──────────────────────────────────────────────────────
    public async Task<IActionResult> TeamCalendar()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        // Get approved leaves for next 60 days
        var from = DateTime.Today;
        var to = DateTime.Today.AddDays(60);

        var leaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId)
                     && r.Approved == true && !r.Cancelled
                     && r.StartDate <= to && r.EndDate >= from)
            .OrderBy(r => r.StartDate)
            .ToListAsync();

        // Conflict detection: same date, multiple employees
        var conflicts = leaves
            .GroupBy(l => l.StartDate.Date)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        ViewBag.Conflicts = conflicts;
        return View(leaves);
    }

    // ─── APPLY LEAVE FOR SELF ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ApplyLeave()
    {
        var leaveTypes = await _context.LeaveTypes.ToListAsync();
        var model = new ApplyLeaveViewModel { LeaveTypes = leaveTypes };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyLeave(ApplyLeaveViewModel model)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        model.LeaveTypes = await _context.LeaveTypes.ToListAsync();

        if (model.StartDate > model.EndDate)
        {
            ModelState.AddModelError("", "Start date cannot be after end date.");
            return View(model);
        }
        if (model.StartDate < DateTime.Today)
        {
            ModelState.AddModelError("", "Leave cannot be applied for past dates.");
            return View(model);
        }

        var overlap = await _context.LeaveRequests
            .AnyAsync(r => r.RequestingEmployeeId == manager.Id && !r.Cancelled
                        && r.Approved != false
                        && r.StartDate <= model.EndDate && r.EndDate >= model.StartDate);
        if (overlap)
        {
            ModelState.AddModelError("", "You already have a leave request overlapping with these dates.");
            return View(model);
        }

        var leaveRequest = new LeaveRequest
        {
            RequestingEmployeeId = manager.Id,
            LeaveTypeId = model.LeaveTypeId,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            DateRequested = DateTime.UtcNow,
            RequestComments = model.Reason,
            Approved = null, // Goes to Admin for approval
            Cancelled = false
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your leave request has been submitted for Admin approval.";
        return RedirectToAction(nameof(Dashboard));
    }

    // ─── REPORTS ────────────────────────────────────────────────────────────
    public async Task<IActionResult> Reports()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var myTeam = _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive);

        var approvedLeaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => myTeam.Any(u => u.Id == r.RequestingEmployeeId) && r.Approved == true && !r.Cancelled)
            .ToListAsync();

        var model = new ManagerReportsViewModel
        {
            UsageByType = approvedLeaves
                .GroupBy(l => l.LeaveType!.Name)
                .Select(g => new LeaveTypeUsage { TypeName = g.Key, TotalDays = g.Sum(l => (int)(l.EndDate - l.StartDate).TotalDays + 1) })
                .ToList(),

            TopUsers = approvedLeaves
                .GroupBy(l => $"{l.RequestingEmployee!.FirstName} {l.RequestingEmployee!.LastName}")
                .Select(g => new EmployeeUsage { Name = g.Key, TotalDays = g.Sum(l => (int)(l.EndDate - l.StartDate).TotalDays + 1) })
                .OrderByDescending(u => u.TotalDays)
                .Take(5)
                .ToList(),

            Trends = approvedLeaves
                .GroupBy(l => l.StartDate.ToString("MMM yyyy"))
                .Select(g => new MonthlyTrend { Month = g.Key, Count = g.Count() })
                .OrderBy(t => DateTime.ParseExact(t.Month, "MMM yyyy", null))
                .ToList()
        };

        return View(model);
    }


    // ─── PROFILE ────────────────────────────────────────────────────────────
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        await _context.Entry(user).Reference(u => u.Team).LoadAsync();
        return View(user);
    }

    // ─── HELPER ─────────────────────────────────────────────────────────────
    private async Task<List<string>> GetTeamMemberIds(string managerId)
    {
        return await _userManager.Users
            .Where(u => u.ManagerId == managerId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();
    }
}
