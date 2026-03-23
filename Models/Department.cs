using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LMS.Models;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? ParentDepartmentId { get; set; }
    
    [ValidateNever]
    public Department? ParentDepartment { get; set; }
    
    [ValidateNever]
    public ICollection<Department> SubDepartments { get; set; } = new List<Department>();

    public string? ManagerId { get; set; }
    
    [ValidateNever]
    public ApplicationUser? Manager { get; set; }
}
