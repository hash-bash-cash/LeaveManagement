using System.ComponentModel.DataAnnotations;

namespace LMS.Models;

public class ApplyLeaveViewModel
{
    public int LeaveTypeId { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today;

    public int TotalDays { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public IFormFile? Attachment { get; set; }

    public List<LeaveType> LeaveTypes { get; set; } = new();
}

public class LeaveBalanceViewModel
{
    public string LeaveTypeName { get; set; } = string.Empty;
    public int Allocated { get; set; }
    public int Used { get; set; }
    public int Remaining => Allocated - Used;
}

public class EmployeeDashboardViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public List<LeaveBalanceViewModel> LeaveBalances { get; set; } = new();
    public List<LeaveRequest> RecentLeaves { get; set; } = new();
    public int PendingRequests { get; set; }
    public int ApprovedThisYear { get; set; }
}
