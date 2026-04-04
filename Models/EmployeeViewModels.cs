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

    // Bereavement Details
    [DataType(DataType.Date)]
    public DateTime? DateOfDeath { get; set; }
    public string? DeceasedName { get; set; }
    public string? DeceasedRelationship { get; set; }
    
    // Flag from the LeaveType configuration
    public bool SupportingDocumentRequired { get; set; }

    public List<LeaveType> LeaveTypes { get; set; } = new();
    public List<Holiday> AvailableFloatingHolidays { get; set; } = new();
}

public class LeaveBalanceViewModel
{
    public string LeaveTypeName { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
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

public class UserProfileViewModel
{
    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Employee Code")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Shift")]
    public string Shift { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Gender")]
    public string Gender { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime? DateOfBirth { get; set; }

    [Required]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Joining Date")]
    public DateTime DateJoined { get; set; }

    public string? DepartmentName { get; set; }
    public string? ManagerName { get; set; }
}
