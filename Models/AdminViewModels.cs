using System.ComponentModel.DataAnnotations;


namespace LMS.Models;

// ─── Admin Dashboard VM ───────────────────────────────────────────────────────
public class AdminDashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int TotalManagers { get; set; }
    public int ActiveLeaveRequests { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalSubDepartments { get; set; }
    public int EmployeesOnLeaveToday { get; set; }
    public int UpcomingHolidays { get; set; }

    public List<LeaveRequest> EmployeesOnLeaveTodayList { get; set; } = new();
    public List<Holiday> UpcomingHolidayList { get; set; } = new();
}

// ─── Admin Leave Request VM ───────────────────────────────────────────────────
public class AdminLeaveRequestViewModel
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public string? Reason { get; set; }
    public DateTime DateRequested { get; set; }
    public string Status { get; set; } = string.Empty; // Pending / Approved / Rejected / Cancelled
    public string? ManagerRemarks { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? DateActioned { get; set; }
    public string? AttachmentPath { get; set; }
    public bool Cancelled { get; set; }
}

// ─── Admin Leave History VM ──────────────────────────────────────────────────
public class AdminLeaveHistoryViewModel
{
    public int OnLeaveTodayCount { get; set; }
    public int PendingApprovalsCount { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalAbsent { get; set; } // Same as on leave today

    public List<AdminLeaveRequestViewModel> TodayLeaves { get; set; } = new();
    public List<AdminLeaveRequestViewModel> PendingLeaves { get; set; } = new();
    public List<DepartmentLeaveStatsViewModel> DepartmentStats { get; set; } = new();
    public List<EmployeeLeaveStatsViewModel> EmployeeStats { get; set; } = new();
    public List<LeaveType> LeaveTypes { get; set; } = new();
}

public class DepartmentLeaveStatsViewModel
{
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalEmployees { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingApprovals { get; set; }
}

public class EmployeeLeaveStatsViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int TotalLeavesTaken { get; set; }
    public int PendingRequests { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
