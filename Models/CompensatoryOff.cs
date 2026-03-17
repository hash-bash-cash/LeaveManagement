using System;

namespace LMS.Models;

public class CompensatoryOff
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    public DateTime DateEarned { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; }
    
    public string Reason { get; set; } = string.Empty;
    
    // The Manager who approved/credited this CO
    public string? ApprovedById { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
}
