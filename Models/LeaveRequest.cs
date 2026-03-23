namespace LMS.Models;

public class LeaveRequest
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateTime DateRequested { get; set; }
    public string RequestComments { get; set; } = string.Empty;

    public bool? Approved { get; set; }
    public bool Cancelled { get; set; }

    // Bereavement Details
    public DateTime? DateOfDeath { get; set; }
    public string? DeceasedName { get; set; }
    public string? DeceasedRelationship { get; set; }
    public bool SupportingDocumentRequired { get; set; }

    public string RequestingEmployeeId { get; set; } = string.Empty;
    public ApplicationUser? RequestingEmployee { get; set; }

    public string? ReviewerId { get; set; }
    public ApplicationUser? Reviewer { get; set; }
    public string? ManagerRemarks { get; set; }
    public DateTime? DateActioned { get; set; }

    public string? AttachmentPath { get; set; }
}
