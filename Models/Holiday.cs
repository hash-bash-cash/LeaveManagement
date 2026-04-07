namespace LMS.Models;

public class Holiday
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsRecurringYearly { get; set; } = true;
    public bool IsFloating { get; set; } = false;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string? EmployeeId { get; set; }
    public ApplicationUser? Employee { get; set; }
}
