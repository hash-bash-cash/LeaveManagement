namespace LMS.Models;

public class LeaveAllocation
{
    public int Id { get; set; }
    public double NumberOfDays { get; set; }
    
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    public int Period { get; set; }
}
