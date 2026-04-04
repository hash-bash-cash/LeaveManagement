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

        // Calculate balance correctly
        var leaveTypes = await _context.LeaveTypes.ToListAsync();
        var allLeaves = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == leave.RequestingEmployeeId && r.Approved == true && !r.Cancelled && r.Id != id)
            .ToListAsync();
        var allocations = await _context.LeaveAllocations
            .Where(a => a.EmployeeId == leave.RequestingEmployeeId && a.Period == DateTime.Now.Year)
            .ToListAsync();
        var cos = await _context.CompensatoryOffs
            .Where(c => c.EmployeeId == leave.RequestingEmployeeId && !c.IsUsed)
            .ToListAsync();

        double available = 0;
        if (leave.LeaveType != null)
        {
            if (leave.LeaveType.Code == "CO") available = cos.Count;
            else if (leave.LeaveType.Code == "LW" || leave.LeaveType.Code == "ML" || leave.LeaveType.Code == "BL") available = 0; // Info only
            else
            {
                var alloc = allocations.FirstOrDefault(a => a.LeaveTypeId == leave.LeaveTypeId);
                if (alloc != null)
                {
                    var usedLeaves = allLeaves.Where(l => l.LeaveTypeId == leave.LeaveTypeId).ToList();
                    double usedDays = 0;
                    foreach(var l in usedLeaves)
                    {
                        double days = 0;
                        for (var d = l.StartDate; d <= l.EndDate; d = d.AddDays(1))
                            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) days++;
                        days -= _context.Holidays.Count(h => h.Date >= l.StartDate && h.Date <= l.EndDate);
                        usedDays += days;
                    }
                    available = Math.Floor(alloc.NumberOfDays - usedDays);
                }
            }
        }

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
            AvailableBalance = (int)available,
            AttachmentPath = leave.AttachmentPath,
            DateOfDeath = leave.DateOfDeath,
            DeceasedName = leave.DeceasedName,
            DeceasedRelationship = leave.DeceasedRelationship
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

    // ─── MY TEAM (CONSULTED HUB: BALANCES + HISTORY) ──────────────────────────
    public async Task<IActionResult> MyTeam()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var teamMembers = await _userManager.Users
            .Where(u => u.ManagerId == manager.Id && u.IsActive)
            .Include(u => u.Department)
            .OrderBy(u => u.FirstName)
            .ToListAsync();

        var result = new List<TeamMemberBalanceViewModel>();

        foreach (var member in teamMembers)
        {
            var allocations = await _context.LeaveAllocations
                .Include(a => a.LeaveType)
                .Where(a => a.EmployeeId == member.Id && a.Period == DateTime.Now.Year)
                .ToListAsync();

            var history = await _context.LeaveRequests
                .Include(h => h.LeaveType)
                .Where(r => r.RequestingEmployeeId == member.Id)
                .OrderByDescending(r => r.DateRequested)
                .ToListAsync();

            var balances = allocations.Select(a => new LeaveBalanceViewModel
            {
                LeaveTypeName = a.LeaveType!.Name,
                LeaveTypeCode = a.LeaveType!.Code,
                Allocated = (int)Math.Floor(a.NumberOfDays),
                Used = (int)Math.Floor(history.Where(l => l.LeaveTypeId == a.LeaveTypeId && l.Approved == true && !l.Cancelled).Sum(l => (l.EndDate - l.StartDate).TotalDays + 1))
            }).ToList();

            result.Add(new TeamMemberBalanceViewModel
            {
                EmployeeId = member.Id,
                EmployeeName = $"{member.FirstName} {member.LastName}",
                Email = member.Email ?? "",
                Shift = member.Shift,
                Balances = balances,
                History = history
            });
        }

        return View(result);
    }

    // ─── CREDIT CO ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> CreditCO()
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();
        
        var myTeam = await _context.Users.Where(u => u.ManagerId == manager.Id && u.IsActive).ToListAsync();
        ViewBag.TeamMembers = myTeam;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreditCO(string employeeId, DateTime dateWorked, string reason)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null) return Challenge();

        var employee = await _context.Users.FirstOrDefaultAsync(u => u.Id == employeeId && u.ManagerId == manager.Id);
        if(employee != null)
        {
            var co = new CompensatoryOff {
                EmployeeId = employeeId,
                DateEarned = dateWorked,
                ExpiryDate = dateWorked.AddMonths(1),
                IsUsed = false,
                Reason = reason,
                ApprovedById = manager.Id
            };
            _context.CompensatoryOffs.Add(co);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Compensatory Off credited successfully.";
        }
        return RedirectToAction(nameof(MyTeam));
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
                LeaveTypeCode = a.LeaveType!.Code,
                Allocated = (int)Math.Floor(a.NumberOfDays),
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
    public IActionResult ApplyLeave()
    {
        return RedirectToAction("Apply", "Employee");
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
                .Select(g => new LeaveTypeUsage { TypeName = g.Key, TotalDays = (int)Math.Floor(g.Sum(l => (l.EndDate - l.StartDate).TotalDays + 1)) })
                .ToList(),

            TopUsers = approvedLeaves
                .GroupBy(l => $"{l.RequestingEmployee!.FirstName} {l.RequestingEmployee!.LastName}")
                .Select(g => new EmployeeUsage { Name = g.Key, TotalDays = (int)Math.Floor(g.Sum(l => (l.EndDate - l.StartDate).TotalDays + 1)) })
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
    [HttpGet]
    public IActionResult Profile()
    {
        return RedirectToAction("Profile", "Employee");
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
