namespace LMS.Models;

public class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DefaultDays { get; set; }
    public bool IsPaid { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public int MaxConsecutiveDays { get; set; }
    public int YearlyLimit { get; set; }
    public bool CarryForward { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
}
