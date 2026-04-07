using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Constants;
using LMS.Data;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Employee + "," + Roles.Manager)]
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

        var allLeaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Where(r => r.RequestingEmployeeId == user.Id)
            .ToListAsync();

        var recentLeaves = allLeaves.OrderByDescending(l => l.DateRequested).Take(5).ToList();

        var balances = await GetLeaveBalancesAsync(user, allLeaves.Where(l => l.Approved == true && !l.Cancelled).ToList());

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
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var leaveTypes = await _context.LeaveTypes
            .Where(lt => lt.Code != "WO" && lt.Code != "HD")
            .ToListAsync();

        var floatingHolidays = await _context.Holidays
            .Where(h => h.IsFloating && ((h.Date.Year == DateTime.Now.Year) || h.IsRecurringYearly))
            .Where(h => (h.DepartmentId == null && h.EmployeeId == null) || (h.DepartmentId == user.DepartmentId) || (h.EmployeeId == user.Id))
            .ToListAsync();

        var model = new ApplyLeaveViewModel { 
            LeaveTypes = leaveTypes,
            AvailableFloatingHolidays = floatingHolidays
        };
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

        // ── Calculate business days (excluding weekends)
        int businessDays = CountBusinessDays(model.StartDate, model.EndDate);

        // ── Validation: Remove holidays from count (Handling recurring and targeted holidays)
        var holidays = await _context.Holidays
            .Where(h => (h.Date >= model.StartDate && h.Date <= model.EndDate) || (h.IsRecurringYearly && h.Date.Month == model.StartDate.Month && h.Date.Day == model.StartDate.Day))
            .Where(h => (h.DepartmentId == null && h.EmployeeId == null) || (h.DepartmentId == user.DepartmentId) || (h.EmployeeId == user.Id))
            .ToListAsync();
        businessDays -= holidays.Count;

        var leaveTypeObj = model.LeaveTypes.FirstOrDefault(lt => lt.Id == model.LeaveTypeId);
        if (leaveTypeObj == null)
        {
            ModelState.AddModelError("", "Invalid leave type.");
            return View(model);
        }

        if (businessDays <= 0 && leaveTypeObj.Code != "FD")
        {
            ModelState.AddModelError("", "Your selected dates fall entirely on weekends or public holidays.");
            return View(model);
        }

        // Rule 1: Advance Notices
        if (leaveTypeObj.Code == "PL" && (model.StartDate.Date - DateTime.Today).TotalDays < 15)
        {
            ModelState.AddModelError("", "Privilege Leave (PL) must be applied at least 15 days in advance.");
            return View(model);
        }
        if (leaveTypeObj.Code == "CL" && (model.StartDate.Date - DateTime.Today).TotalDays < 3)
        {
            ModelState.AddModelError("", "Casual Leave (CL) must be applied at least 3 days in advance.");
            return View(model);
        }
        if (leaveTypeObj.Code == "CO" && (model.StartDate.Date - DateTime.Today).TotalDays < 3)
        {
            ModelState.AddModelError("", "Compensatory Off (CO) must be applied at least 3 days in advance.");
            return View(model);
        }

        // Not in the past (Rule 3)
        if (model.StartDate < DateTime.Today)
        {
            if (leaveTypeObj.Code == "SL")
            {
                if ((DateTime.Today - model.StartDate.Date).TotalDays > 3)
                {
                    ModelState.AddModelError("", "Sick Leave (SL) cannot be applied for more than 3 days in the past.");
                    return View(model);
                }
            }
            else
            {
                ModelState.AddModelError("", "Leave cannot be applied for past dates unless it is Sick Leave.");
                return View(model);
            }
        }

        // Rule 4: SL > 2 days need medical cert
        if (leaveTypeObj.Code == "SL" && businessDays > 2 && model.Attachment == null)
        {
            ModelState.AddModelError("", "Sick Leave for more than 2 days requires a medical certificate to be attached.");
            return View(model);
        }

        // Rule 5: CL max 2 days
        if (leaveTypeObj.Code == "CL" && businessDays > 2)
        {
            ModelState.AddModelError("", "Casual Leave (CL) can be consumed up to a maximum of 2 days at each instance.");
            return View(model);
        }

        // Rule 2: CL clubbing
        if (leaveTypeObj.Code == "CL")
        {
            var hasAdjacentHoliday = await _context.Holidays.AnyAsync(h => h.Date == model.StartDate.AddDays(-1) || h.Date == model.EndDate.AddDays(1) || (h.Date >= model.StartDate && h.Date <= model.EndDate));
            var hasAdjacentLeave = await _context.LeaveRequests.AnyAsync(r => r.RequestingEmployeeId == user.Id && r.Approved != false && !r.Cancelled && (r.EndDate.Date == model.StartDate.AddDays(-1).Date || r.StartDate.Date == model.EndDate.AddDays(1).Date));
            if (hasAdjacentHoliday || hasAdjacentLeave)
            {
                ModelState.AddModelError("", "Casual Leave (CL) cannot be clubbed with any other leave type or holidays.");
                return View(model);
            }
        }

        // Rule 7: PL first 2 months
        if (leaveTypeObj.Code == "PL" && (DateTime.Today - user.DateJoined).TotalDays < 60)
        {
            ModelState.AddModelError("", "Privilege Leave (PL) cannot be availed in the first two months of employment.");
            return View(model);
        }

        // Rule 10: Maternity Leave
        if (leaveTypeObj.Code == "ML")
        {
            if (user.Gender?.ToLower() != "female")
            {
                ModelState.AddModelError("", "Maternity Leave is only available for female employees.");
                return View(model);
            }
            if ((DateTime.Today - user.DateJoined).TotalDays < 112)
            {
                int businessDaysWorked = CountBusinessDays(user.DateJoined, DateTime.Today);
                if (businessDaysWorked < 80)
                {
                    ModelState.AddModelError("", "Must have worked at least 80 business days prior to Maternity Leave.");
                    return View(model);
                }
            }
            if ((model.EndDate - model.StartDate).TotalDays < 181)
            {
                ModelState.AddModelError("", "Maternity Leave must be consumed as 26 weeks (182 days) at once.");
                return View(model);
            }
            if (model.Attachment == null)
            {
                ModelState.AddModelError("", "Maternity Leave requires a verifiable doctor's certificate.");
                return View(model);
            }
        }

        // Rule 10b: Bereavement
        if (leaveTypeObj.Code == "BL")
        {
            if (string.IsNullOrEmpty(model.DeceasedName) || string.IsNullOrEmpty(model.DeceasedRelationship) || model.DateOfDeath == null)
            {
                ModelState.AddModelError("", "Deceased person's name, relationship, and date of death must be provided for Bereavement Leave.");
                return View(model);
            }

            // BL Eligibility Criteria (Categorized)
            int maxDays = 3; // Default for Category B
            var rel = model.DeceasedRelationship;

            var catA = new[] { "Parent", "Spouse", "Child" };
            var catC = new[] { "Brother-in-law", "Sister-in-law", "Spouse Grand Parent" };

            if (catA.Contains(rel)) maxDays = 5;
            else if (catC.Contains(rel)) maxDays = 1;
            else maxDays = 3; // Category B (Brother, Sister, Grand Parent/Child, In-laws Parents/Children)

            if (businessDays > maxDays)
            {
                ModelState.AddModelError("", $"Maximum Bereavement Leave for '{rel}' is {maxDays} day(s). You requested {businessDays} days.");
                return View(model);
            }
            
            // Note: No document required for BL as per latest requirement.
        }

        // Rule 11: Study or Medical certs
        if (model.Reason.ToLower().Contains("medical") && leaveTypeObj.Code != "SL" && leaveTypeObj.Code != "ML" && model.Attachment == null)
        {
             ModelState.AddModelError("", "Leave with medical reason requires a verifiable doctor's certificate.");
             return View(model);
        }
        if (model.Reason.ToLower().Contains("study") && model.Attachment == null)
        {
             ModelState.AddModelError("", "Leave for study purposes requires a verifiable exam schedule.");
             return View(model);
        }

        // Rule 15: FD
        if (leaveTypeObj.Code == "FD")
        {
            var isFloatingHoliday = holidays.Any(h => h.IsFloating && 
                (h.Date.Date == model.StartDate.Date || (h.IsRecurringYearly && h.Date.Month == model.StartDate.Month && h.Date.Day == model.StartDate.Day)));
            
            if (!isFloatingHoliday)
            {
                 ModelState.AddModelError("", "Floating Holiday can only be applied on designated Floating Holiday dates.");
                 return View(model);
            }
            businessDays = 1; // FD is always 1 day
        }

        // Rule 12: Attendance
        if (user.DepartmentId.HasValue)
        {
            var deptUsers = await _context.Users.Where(u => u.DepartmentId == user.DepartmentId && u.IsActive).CountAsync();
            if (deptUsers > 0)
            {
                for (var d = model.StartDate; d <= model.EndDate; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;

                    var othersOnLeave = await _context.LeaveRequests
                        .Include(r => r.RequestingEmployee)
                        .Where(r => r.RequestingEmployee!.DepartmentId == user.DepartmentId && r.Approved != false && !r.Cancelled && r.StartDate <= d && r.EndDate >= d)
                        .CountAsync();
                    
                    double attendancePercentage = (double)(deptUsers - othersOnLeave - 1) / deptUsers * 100;
                    if (attendancePercentage < 60)
                    {
                        ModelState.AddModelError("", $"Your leave request on {d:dd MMM yyyy} drops department attendance below 60% ({attendancePercentage:0.0}%). Request rejected.");
                        return View(model);
                    }
                }
            }
        }

        // ── Validation: Check leave balance (Skip for CO, LWP, BL, ML as they might have different rules or no explicit allocation)
        if (leaveTypeObj.Code != "CO" && leaveTypeObj.Code != "LW" && leaveTypeObj.Code != "BL" && leaveTypeObj.Code != "ML")
        {
            var allocation = await _context.LeaveAllocations
                .FirstOrDefaultAsync(a => a.EmployeeId == user.Id
                                       && a.LeaveTypeId == model.LeaveTypeId
                                       && a.Period == DateTime.Now.Year);

            if (allocation == null)
            {
                ModelState.AddModelError("", "You have no leave allocation for this leave type. Please contact HR.");
                return View(model);
            }

            var usedRequests = await _context.LeaveRequests
                .Where(r => r.RequestingEmployeeId == user.Id
                         && r.LeaveTypeId == model.LeaveTypeId
                         && r.Approved == true
                         && !r.Cancelled)
                .ToListAsync();

            var usedDays = usedRequests.Sum(r => (r.EndDate - r.StartDate).TotalDays + 1);

            if (usedDays + businessDays > Math.Floor(allocation.NumberOfDays))
            {
                ModelState.AddModelError("", $"Insufficient leave balance. Available: {(int)Math.Floor(allocation.NumberOfDays - usedDays)} days. Requested: {businessDays} days.");
                return View(model);
            }
        }
        else if (leaveTypeObj.Code == "CO")
        {
             // Check if user has unused COs valid for the requested dates // Should be Count of unused COs must be >= businessDays
             var unusedCOs = await _context.CompensatoryOffs.Where(c => c.EmployeeId == user.Id && !c.IsUsed && c.ExpiryDate >= model.EndDate).ToListAsync();
             if (unusedCOs.Count < businessDays)
             {
                 ModelState.AddModelError("", $"Insufficient Compensatory Off balance. Available: {unusedCOs.Count} days. Requested: {businessDays} days.");
                 return View(model);
             }
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
            AttachmentPath = attachmentPath,
            
            // Bereavement Details
            DateOfDeath = model.DateOfDeath,
            DeceasedName = model.DeceasedName,
            DeceasedRelationship = model.DeceasedRelationship,
            SupportingDocumentRequired = model.SupportingDocumentRequired
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        // Notify Manager
        if (!string.IsNullOrEmpty(user.ManagerId))
        {
            var leaveType = await _context.LeaveTypes.FindAsync(model.LeaveTypeId);
            var notification = new Notification
            {
                UserId = user.ManagerId,
                Title = "New Leave Request",
                Message = $"{user.FirstName} {user.LastName} has applied for {leaveType?.Name} starts from {model.StartDate:dd MMM yyyy}.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

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

        var allLeaves = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == user.Id && r.Approved == true && !r.Cancelled)
            .ToListAsync();
        
        var balances = await GetLeaveBalancesAsync(user, allLeaves);

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

    // ─── LEAVE POLICIES ─────────────────────────────────────
    public async Task<IActionResult> LeavePolicies()
    {
        var leaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync();
        return View(leaveTypes);
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
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        await _context.Entry(user).Reference(u => u.Manager).LoadAsync();

        var model = new UserProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            EmployeeCode = user.EmployeeCode,
            Phone = user.Phone,
            Shift = user.Shift,
            Gender = user.Gender,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            DateJoined = user.DateJoined,
            DepartmentName = user.Department?.Name,
            ManagerName = user.Manager != null ? $"{user.Manager.FirstName} {user.Manager.LastName}" : "No Manager Assigned"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UserProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (ModelState.IsValid)
        {
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Phone = model.Phone;
            user.Shift = model.Shift;
            user.Gender = model.Gender;
            user.DateOfBirth = model.DateOfBirth;
            user.Address = model.Address;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender ?? string.Empty;
        user.Address = model.Address ?? string.Empty;
        user.Phone = model.Phone ?? string.Empty;
        user.Shift = model.Shift ?? string.Empty;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Profile updated successfully!";
        }
        else
        {
            TempData["Error"] = "Failed to update profile. " + string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Profile));
    }

    // ─── HELPERS ────────────────────────────────────────────
    private async Task<List<LeaveBalanceViewModel>> GetLeaveBalancesAsync(ApplicationUser user, List<LeaveRequest> allApprovedLeaves)
    {
        var leaveTypes = await _context.LeaveTypes.ToListAsync();
        var allocations = await _context.LeaveAllocations
            .Where(a => a.EmployeeId == user.Id && a.Period == DateTime.Now.Year)
            .ToListAsync();
        var cos = await _context.CompensatoryOffs
            .Where(c => c.EmployeeId == user.Id)
            .ToListAsync();
        var holidays = await _context.Holidays.ToListAsync();

        var balances = new List<LeaveBalanceViewModel>();

        foreach (var type in leaveTypes)
        {
             var typeLeaves = allApprovedLeaves.Where(l => l.LeaveTypeId == type.Id).ToList();
             double used = typeLeaves.Sum(l => (double)CountBusinessDays(l.StartDate, l.EndDate) - holidays.Count(h => h.Date >= l.StartDate && h.Date <= l.EndDate));
             
             var alloc = allocations.FirstOrDefault(a => a.LeaveTypeId == type.Id);
             
             if (type.Code == "CO")
             {
                  balances.Add(new LeaveBalanceViewModel {
                       LeaveTypeName = type.Name,
                       LeaveTypeCode = type.Code,
                       Allocated = cos.Count,
                       Used = cos.Count(c => c.IsUsed)
                  });
             }
             else if (type.Code == "LW" || type.Code == "ML" || type.Code == "BL")
             {
                  balances.Add(new LeaveBalanceViewModel {
                       LeaveTypeName = type.Name,
                       LeaveTypeCode = type.Code,
                       Allocated = 0,
                       Used = (int)Math.Floor(used)
                  });
             }
             else if (alloc != null)
             {
                  balances.Add(new LeaveBalanceViewModel {
                       LeaveTypeName = type.Name,
                       LeaveTypeCode = type.Code,
                       Allocated = (int)Math.Floor(alloc.NumberOfDays),
                       Used = (int)Math.Floor(used)
                  });
             }
        }
        return balances;
    }

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
