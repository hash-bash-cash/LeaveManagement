using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Constants;
using LMS.Data;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Employee + "," + Roles.Manager + "," + Roles.Admin)]
public class EmployeeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ─── DASHBOARD ──────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var allocations = await _context.LeaveAllocations
            .Include(a => a.LeaveType)
            .Where(a => a.EmployeeId == user.Id && a.Period == DateTime.Now.Year)
            .ToListAsync();

        var allLeaves = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == user.Id)
            .ToListAsync();

        var recentLeaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Where(r => r.RequestingEmployeeId == user.Id)
            .OrderByDescending(r => r.DateRequested)
            .Take(5)
            .ToListAsync();

        var balances = allocations.Select(a => new LeaveBalanceViewModel
        {
            LeaveTypeName = a.LeaveType!.Name,
            Allocated = a.NumberOfDays,
            Used = allLeaves.Count(l => l.LeaveTypeId == a.LeaveTypeId && l.Approved == true && !l.Cancelled)
        }).ToList();

        var model = new EmployeeDashboardViewModel
        {
            EmployeeName = $"{user.FirstName} {user.LastName}",
            LeaveBalances = balances,
            RecentLeaves = recentLeaves,
            PendingRequests = allLeaves.Count(l => l.Approved == null && !l.Cancelled),
            ApprovedThisYear = allLeaves.Count(l => l.Approved == true && !l.Cancelled && l.DateRequested.Year == DateTime.Now.Year)
        };

        return View(model);
    }

    // ─── APPLY LEAVE ────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Apply()
    {
        var leaveTypes = await _context.LeaveTypes.ToListAsync();
        var model = new ApplyLeaveViewModel { LeaveTypes = leaveTypes };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyLeaveViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        model.LeaveTypes = await _context.LeaveTypes.ToListAsync();

        // ── Validation: Date order
        if (model.StartDate > model.EndDate)
        {
            ModelState.AddModelError("", "Start date cannot be after end date.");
            return View(model);
        }

        // ── Validation: Not in the past
        if (model.StartDate < DateTime.Today)
        {
            ModelState.AddModelError("", "Leave cannot be applied for past dates.");
            return View(model);
        }

        // ── Calculate business days (excluding weekends)
        int businessDays = CountBusinessDays(model.StartDate, model.EndDate);

        // ── Validation: Remove holidays from count
        var holidays = await _context.Holidays
            .Where(h => h.Date >= model.StartDate && h.Date <= model.EndDate)
            .ToListAsync();
        businessDays -= holidays.Count;

        if (businessDays <= 0)
        {
            ModelState.AddModelError("", "Your selected dates fall entirely on weekends or public holidays.");
            return View(model);
        }

        // ── Validation: Check leave balance
        var allocation = await _context.LeaveAllocations
            .FirstOrDefaultAsync(a => a.EmployeeId == user.Id
                                   && a.LeaveTypeId == model.LeaveTypeId
                                   && a.Period == DateTime.Now.Year);

        if (allocation == null)
        {
            ModelState.AddModelError("", "You have no leave allocation for this leave type. Please contact HR.");
            return View(model);
        }

        var usedDays = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == user.Id
                     && r.LeaveTypeId == model.LeaveTypeId
                     && r.Approved == true
                     && !r.Cancelled)
            .SumAsync(r => (int)(r.EndDate - r.StartDate).TotalDays + 1);

        if (usedDays + businessDays > allocation.NumberOfDays)
        {
            ModelState.AddModelError("", $"Insufficient leave balance. Available: {allocation.NumberOfDays - usedDays} days. Requested: {businessDays} days.");
            return View(model);
        }

        // ── Validation: Overlap check
        var overlap = await _context.LeaveRequests
            .AnyAsync(r => r.RequestingEmployeeId == user.Id
                        && !r.Cancelled
                        && r.Approved != false
                        && r.StartDate <= model.EndDate
                        && r.EndDate >= model.StartDate);
        if (overlap)
        {
            ModelState.AddModelError("", "You already have a leave request overlapping with these dates.");
            return View(model);
        }

        // ── Handle Attachment Upload
        string? attachmentPath = null;
        if (model.Attachment != null)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Attachment.FileName);
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "attachments");
            if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
            
            var filePath = Path.Combine(uploadsPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.Attachment.CopyToAsync(stream);
            }
            attachmentPath = "/uploads/attachments/" + fileName;
        }

        // ── Create Request
        var leaveRequest = new LeaveRequest
        {
            RequestingEmployeeId = user.Id,
            LeaveTypeId = model.LeaveTypeId,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            DateRequested = DateTime.UtcNow,
            RequestComments = model.Reason,
            Approved = null,
            Cancelled = false,
            AttachmentPath = attachmentPath
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave application submitted successfully! Awaiting manager approval.";
        return RedirectToAction(nameof(History));
    }

    // ─── LEAVE HISTORY ──────────────────────────────────────
    public async Task<IActionResult> History()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var leaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Reviewer)
            .Where(r => r.RequestingEmployeeId == user.Id)
            .OrderByDescending(r => r.DateRequested)
            .ToListAsync();

        return View(leaves);
    }

    // ─── LEAVE BALANCE ──────────────────────────────────────
    public async Task<IActionResult> Balance()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var allocations = await _context.LeaveAllocations
            .Include(a => a.LeaveType)
            .Where(a => a.EmployeeId == user.Id && a.Period == DateTime.Now.Year)
            .ToListAsync();

        var allLeaves = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == user.Id && r.Approved == true && !r.Cancelled)
            .ToListAsync();

        var balances = allocations.Select(a => new LeaveBalanceViewModel
        {
            LeaveTypeName = a.LeaveType!.Name,
            Allocated = a.NumberOfDays,
            Used = allLeaves.Count(l => l.LeaveTypeId == a.LeaveTypeId)
        }).ToList();

        return View(balances);
    }

    // ─── CANCEL LEAVE ───────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var leave = await _context.LeaveRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestingEmployeeId == user.Id);

        if (leave == null) return NotFound();

        if (leave.Approved == true && leave.StartDate <= DateTime.Today)
        {
            TempData["Error"] = "Cannot cancel a leave that has already started.";
            return RedirectToAction(nameof(History));
        }

        leave.Cancelled = true;
        leave.Approved = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request cancelled successfully.";
        return RedirectToAction(nameof(History));
    }

    // ─── HOLIDAYS ───────────────────────────────────────────
    public async Task<IActionResult> Holidays()
    {
        var holidays = await _context.Holidays
            .OrderBy(h => h.Date)
            .ToListAsync();
        return View(holidays);
    }

    // ─── PROFILE ────────────────────────────────────────────
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        await _context.Entry(user).Reference(u => u.Team).LoadAsync();

        return View(user);
    }

    // ─── HELPERS ────────────────────────────────────────────
    private static int CountBusinessDays(DateTime start, DateTime end)
    {
        int count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }
}
