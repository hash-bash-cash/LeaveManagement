namespace LMS.Models;

public class ManagerDashboardViewModel
{
    public string ManagerName { get; set; } = string.Empty;
    public int PendingRequests { get; set; }
    public int TeamMembersOnLeaveToday { get; set; }
    public int TotalTeamMembers { get; set; }
    public int UpcomingLeaveCount { get; set; }
    public List<LeaveRequest> PendingLeaveRequests { get; set; } = new();
    public List<ApplicationUser> TeamOnLeaveToday { get; set; } = new();
    public List<LeaveRequest> UpcomingLeaves { get; set; } = new();
}

public class TeamMemberBalanceViewModel
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public List<LeaveBalanceViewModel> Balances { get; set; } = new();
}

public class LeaveApprovalViewModel
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime DateRequested { get; set; }
    public string? ManagerRemarks { get; set; }
    public int AvailableBalance { get; set; }
    public string? AttachmentPath { get; set; }
}

public class ManagerReportsViewModel
{
    public List<LeaveTypeUsage> UsageByType { get; set; } = new();
    public List<EmployeeUsage> TopUsers { get; set; } = new();
    public List<MonthlyTrend> Trends { get; set; } = new();
}

public class LeaveTypeUsage
{
    public string TypeName { get; set; } = string.Empty;
    public int TotalDays { get; set; }
}

public class EmployeeUsage
{
    public string Name { get; set; } = string.Empty;
    public int TotalDays { get; set; }
}

public class MonthlyTrend
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}
