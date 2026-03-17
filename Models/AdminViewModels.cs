using LMS.Models;

namespace LMS.Models;

// ─── Admin Dashboard VM ───────────────────────────────────────────────────────
public class AdminDashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int TotalManagers { get; set; }
    public int ActiveLeaveRequests { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalTeams { get; set; }
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
