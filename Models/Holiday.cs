namespace LMS.Models;

public class Holiday
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsRecurringYearly { get; set; } = true;
    public bool IsFloating { get; set; } = false;
}
