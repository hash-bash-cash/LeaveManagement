using Microsoft.AspNetCore.Identity;

namespace LMS.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime DateJoined { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;

    // Registration Details
    public string EmployeeCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;

    // Organization Hierarchy
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }


    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }

    // Account Approval State
    public bool IsActive { get; set; } = false;
    public UserStatus Status { get; set; } = UserStatus.Pending;
}
