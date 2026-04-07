using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Data;
using LMS.Constants;
using ClosedXML.Excel;
namespace LMS.Controllers;

[Authorize]
public class HolidaysController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HolidaysController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Holidays/Manage (Formerly Index)
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Manage()
    {
        var holidays = await _context.Holidays
            .Include(h => h.Department)
            .Include(h => h.Employee)
            .OrderByDescending(h => h.Date)
            .ToListAsync();
        return View("Index", holidays);
    }

    // GET: Holidays/Calendar
    public IActionResult Index()
    {
        return View("Calendar");
    }

    // GET: Holidays/Create
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create()
    {
        ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name");
        ViewData["EmployeeId"] = new SelectList(_context.Users.Where(u => u.IsActive), "Id", "Email"); // Using Email for clarity
        return View();
    }

    // POST: Holidays/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Holiday holiday)
    {
        if (ModelState.IsValid)
        {
            _context.Add(holiday);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", holiday.DepartmentId);
        ViewData["EmployeeId"] = new SelectList(_context.Users.Where(u => u.IsActive), "Id", "Email", holiday.EmployeeId);
        return View(holiday);
    }

    // GET: Holidays/Import
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Import()
    {
        ViewData["Departments"] = _context.Departments.ToList();
        return View();
    }

    // POST: Holidays/Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Import(IFormFile excelFile, List<int> departmentIds)
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            ModelState.AddModelError("", "Please upload a valid Excel file.");
            ViewData["Departments"] = _context.Departments.ToList();
            return View();
        }

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) throw new Exception("No worksheets found in the Excel file.");

            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1); // Skip header row
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var name = row.Cell(1).GetValue<string>();
                    
                    DateTime date;
                    if (!row.Cell(2).TryGetValue<DateTime>(out date))
                    {
                        if (!DateTime.TryParse(row.Cell(2).GetString(), out date))
                        {
                            continue; // Skip if date is invalid
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string recStr = row.Cell(3).GetString()?.ToLower() ?? "";
                        string floatStr = row.Cell(4).GetString()?.ToLower() ?? "";
                        
                        bool isRecurring = recStr == "yes" || recStr == "true" || recStr == "1";
                        bool isFloating = floatStr == "yes" || floatStr == "true" || floatStr == "1";

                        if (departmentIds != null && departmentIds.Any())
                        {
                            foreach (var deptId in departmentIds)
                            {
                                _context.Holidays.Add(new Holiday
                                {
                                    Name = name,
                                    Date = date,
                                    IsRecurringYearly = isRecurring,
                                    IsFloating = isFloating,
                                    DepartmentId = deptId
                                });
                            }
                        }
                        else
                        {
                            // Global holiday
                            _context.Holidays.Add(new Holiday
                            {
                                Name = name,
                                Date = date,
                                IsRecurringYearly = isRecurring,
                                IsFloating = isFloating,
                                DepartmentId = null
                            });
                        }
                    }
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Holidays imported successfully.";
                return RedirectToAction(nameof(Manage));
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Error processing file. Please ensure it has the correct format. Details: " + ex.Message);
        }

        ViewData["Departments"] = _context.Departments.ToList();
        return View();
    }

    // GET: Holidays/Edit/5
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday == null) return NotFound();

        ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", holiday.DepartmentId);
        ViewData["EmployeeId"] = new SelectList(_context.Users.Where(u => u.IsActive), "Id", "Email", holiday.EmployeeId);
        return View(holiday);
    }

    // POST: Holidays/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id, Holiday holiday)
    {
        if (id != holiday.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(holiday);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HolidayExists(holiday.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Manage));
        }
        ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", holiday.DepartmentId);
        ViewData["EmployeeId"] = new SelectList(_context.Users.Where(u => u.IsActive), "Id", "Email", holiday.EmployeeId);
        return View(holiday);
    }

    // GET: Holidays/Delete/5
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var holiday = await _context.Holidays
            .Include(h => h.Department)
            .Include(h => h.Employee)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (holiday == null) return NotFound();

        return View(holiday);
    }

    // POST: Holidays/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday != null)
        {
            _context.Holidays.Remove(holiday);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Manage));
    }

    private bool HolidayExists(int id)
    {
        return _context.Holidays.Any(e => e.Id == id);
    }

    // API for Calendar
    [HttpGet]
    public async Task<JsonResult> GetCalendarData(int month, int year)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new List<object>());

        // 1. Fetch holidays
        var holidays = await _context.Holidays
            .Where(h => (h.Date.Month == month && (h.Date.Year == year || h.IsRecurringYearly)))
            .ToListAsync();

        // ── Filter holidays for the user
        var userHolidays = holidays.Where(h =>
            (h.DepartmentId == null && h.EmployeeId == null) || // Global
            (h.DepartmentId == user.DepartmentId) ||            // Department specific
            (h.EmployeeId == user.Id)                            // Employee specific
        ).ToList();

        var holidayList = userHolidays.Select(h => new
        {
            title = h.Name + (h.IsFloating ? " (Floating)" : ""),
            start = (h.IsRecurringYearly ? new DateTime(year, h.Date.Month, h.Date.Day) : h.Date).ToString("yyyy-MM-dd"),
            backgroundColor = h.IsFloating ? "#ffc107" : "#0d6efd",
            borderColor = h.IsFloating ? "#e0a800" : "#0056b3",
            textColor = "#fff",
            type = "holiday"
        }).ToList<object>();

        // 2. Fetch leaves
        var leavesQuery = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Where(r => r.Approved == true && !r.Cancelled);

        if (User.IsInRole(Roles.Manager))
        {
            // Manager sees their team's leaves + their own
            leavesQuery = leavesQuery.Where(r => r.RequestingEmployee.ManagerId == user.Id || r.RequestingEmployeeId == user.Id);
        }
        else if (User.IsInRole(Roles.Admin))
        {
            // Admin sees all? Let's limit it to the current view month/year for performance
            leavesQuery = leavesQuery.Where(l => (l.StartDate.Month == month && l.StartDate.Year == year) || (l.EndDate.Month == month && l.EndDate.Year == year));
        }
        else
        {
            // Employee only sees their own
            leavesQuery = leavesQuery.Where(r => r.RequestingEmployeeId == user.Id);
        }

        var leaves = await leavesQuery.ToListAsync();
        var leaveList = leaves.Select(l => new
        {
            title = (User.IsInRole(Roles.Manager) || User.IsInRole(Roles.Admin) ? $"{l.RequestingEmployee?.FirstName}: " : "") + l.LeaveType?.Name,
            start = l.StartDate.ToString("yyyy-MM-dd"),
            end = l.EndDate.AddDays(1).ToString("yyyy-MM-dd"), // FullCalendar end exclusive
            backgroundColor = "#28a745",
            borderColor = "#1e7e34",
            textColor = "#fff",
            type = "leave"
        });

        holidayList.AddRange(leaveList);

        return Json(holidayList);
    }
}
