using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Data;

namespace LMS.Services;

public class LeaveAllocationService(ApplicationDbContext context) : ILeaveAllocationService
{
    private readonly ApplicationDbContext _context = context;

    public async Task AllocateDefaultLeavesAsync(string userId, int year)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return;

        var leaveTypes = await _context.LeaveTypes
            .Where(t => t.DefaultDays > 0 && t.IsEnabled)
            .ToListAsync();

        foreach (var type in leaveTypes)
        {
            var hasAllocation = await _context.LeaveAllocations
                .AnyAsync(a => a.EmployeeId == userId && a.LeaveTypeId == type.Id && a.Period == year);

            if (!hasAllocation)
            {
                double days = type.DefaultDays;
                if (type.Code == "PL")
                {
                    days = 1; // Starting with 1 PL as per new policy
                }

                _context.LeaveAllocations.Add(new LeaveAllocation
                {
                    EmployeeId = userId,
                    LeaveTypeId = type.Id,
                    NumberOfDays = days,
                    Period = year,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task AllocateDefaultLeavesToAllActiveUsersAsync(int year)
    {
        var leaveTypes = await _context.LeaveTypes
            .Where(t => t.DefaultDays > 0 && t.IsEnabled)
            .ToListAsync();

        var activeUsers = await _context.Users.Where(u => u.IsActive).ToListAsync();

        foreach (var user in activeUsers)
        {
            foreach (var type in leaveTypes)
            {
                var hasAllocation = await _context.LeaveAllocations
                    .AnyAsync(a => a.EmployeeId == user.Id && a.LeaveTypeId == type.Id && a.Period == year);

                if (!hasAllocation)
                {
                    double days = type.DefaultDays;
                    if (type.Code == "PL")
                    {
                        days = 1; // Starting with 1 PL as per new policy
                    }

                    _context.LeaveAllocations.Add(new LeaveAllocation
                    {
                        EmployeeId = user.Id,
                        LeaveTypeId = type.Id,
                        NumberOfDays = days,
                        Period = year,
                        DateCreated = DateTime.UtcNow,
                        DateModified = DateTime.UtcNow
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
