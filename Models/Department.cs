namespace LMS.Models;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public ICollection<Department> SubDepartments { get; set; } = new List<Department>();

    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }

    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
