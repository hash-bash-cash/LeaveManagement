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
            TotalDepartments      = await _context.Departments.CountAsync(),
            TotalTeams            = await _context.Teams.CountAsync(),
            EmployeesOnLeaveToday = onLeaveToday.Count,
            UpcomingHolidays      = upcomingHolidays.Count,
            EmployeesOnLeaveTodayList = onLeaveToday,
            UpcomingHolidayList       = upcomingHolidays
        };

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

    // ─── APPROVED LEAVES ────────────────────────────────────────────────────
    public async Task<IActionResult> ApprovedLeaves()
    {
        var leaves = await GetMappedLeaves("Approved");
        return View(leaves);
    }

    // ─── REJECTED LEAVES ────────────────────────────────────────────────────
    public async Task<IActionResult> RejectedLeaves()
    {
        var leaves = await GetMappedLeaves("Rejected");
        return View(leaves);
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

    // ─── HOLIDAY CALENDAR ───────────────────────────────────────────────────
    public async Task<IActionResult> HolidayCalendar()
    {
        var holidays = await _context.Holidays.OrderBy(h => h.Date).ToListAsync();
        return View(holidays);
    }

    [HttpGet]
    public IActionResult AddHoliday() => View(new Holiday());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHoliday(Holiday model)
    {
        if (ModelState.IsValid)
        {
            _context.Holidays.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday added successfully.";
            return RedirectToAction(nameof(HolidayCalendar));
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditHoliday(int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday == null) return NotFound();
        return View(holiday);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHoliday(int id, Holiday model)
    {
        if (id != model.Id) return NotFound();
        if (ModelState.IsValid)
        {
            _context.Update(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday updated successfully.";
            return RedirectToAction(nameof(HolidayCalendar));
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday != null)
        {
            _context.Holidays.Remove(holiday);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday deleted.";
        }
        return RedirectToAction(nameof(HolidayCalendar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportHolidays(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please upload a valid CSV file.";
            return RedirectToAction(nameof(HolidayCalendar));
        }

        int added = 0;
        int errors = 0;

        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        string? line;
        bool firstLine = true;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (firstLine) { firstLine = false; continue; } // skip header
            var parts = line.Split(',');
            if (parts.Length < 2) { errors++; continue; }

            var name = parts[0].Trim().Trim('"');
            if (DateTime.TryParse(parts[1].Trim().Trim('"'), out var date))
            {
                bool recurring = parts.Length > 2 && parts[2].Trim().ToLower() == "true";
                _context.Holidays.Add(new Holiday { Name = name, Date = date, IsRecurringYearly = recurring });
                added++;
            }
            else { errors++; }
        }

        if (added > 0) await _context.SaveChangesAsync();

        TempData["Success"] = $"Imported {added} holidays. {(errors > 0 ? $"{errors} rows skipped." : "")}";
        return RedirectToAction(nameof(HolidayCalendar));
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
