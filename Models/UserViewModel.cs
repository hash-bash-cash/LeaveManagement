namespace LMS.Models;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    
    // Additional fields for Review logic
    public string EmployeeCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? TeamId { get; set; }
    public string? ManagerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
