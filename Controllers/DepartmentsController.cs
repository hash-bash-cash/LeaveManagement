using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Data;
using LMS.Models;
using LMS.Constants;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class DepartmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DepartmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ─── INDEX ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var departments = await _context.Departments
            .Include(d => d.SubDepartments)
            .Include(d => d.Manager)
            .Include(d => d.ParentDepartment)
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View(departments);
    }

    // ─── CREATE ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBags();
        return View(new Department());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Department model)
    {
        if (ModelState.IsValid)
        {
            _context.Departments.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Department created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // Debug: Log validation errors to TempData
        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        TempData["Error"] = "Validation failed: " + string.Join("; ", errors);
        
        await PopulateViewBags();
        return View(model);
    }

    // ─── EDIT ────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var dept = await _context.Departments.FindAsync(id);
        if (dept == null) return NotFound();
        await PopulateViewBags(excludeId: id);
        return View(dept);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Department model)
    {
        if (id != model.Id) return NotFound();

        // Prevent a department from being its own parent
        if (model.ParentDepartmentId == id)
        {
            ModelState.AddModelError("", "A department cannot be its own parent.");
        }

        if (ModelState.IsValid)
        {
            _context.Update(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Department updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        await PopulateViewBags(excludeId: id);
        return View(model);
    }

    // ─── DELETE ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var dept = await _context.Departments
            .Include(d => d.SubDepartments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dept == null) return NotFound();

        if (dept.SubDepartments.Any())
        {
            TempData["Error"] = "Cannot delete a department that has sub-departments. Remove sub-departments first.";
            return RedirectToAction(nameof(Index));
        }

        var hasEmployees = await _context.Set<ApplicationUser>()
            .AnyAsync(u => u.DepartmentId == id);
        if (hasEmployees)
        {
            TempData["Error"] = "Cannot delete a department with assigned employees. Reassign employees first.";
            return RedirectToAction(nameof(Index));
        }

        _context.Departments.Remove(dept);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Department deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ─── ASSIGN MANAGER ──────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> AssignManager(int id)
    {
        var dept = await _context.Departments
            .Include(d => d.Manager)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dept == null) return NotFound();

        var managers = await _userManager.GetUsersInRoleAsync(Roles.Manager);
        ViewBag.Managers = managers.Where(m => m.IsActive).ToList();
        return View(dept);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignManager(int id, string? managerId)
    {
        var dept = await _context.Departments.FindAsync(id);
        if (dept == null) return NotFound();

        dept.ManagerId = string.IsNullOrEmpty(managerId) ? null : managerId;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Manager assigned successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ─── HELPER ──────────────────────────────────────────────────────────────
    private async Task PopulateViewBags(int? excludeId = null)
    {
        var depts = await _context.Departments.ToListAsync();
        if (excludeId.HasValue)
            depts = depts.Where(d => d.Id != excludeId.Value).ToList();

        ViewBag.Departments = depts;

        var managers = await _userManager.GetUsersInRoleAsync(Roles.Manager);
        ViewBag.Managers = managers.Where(m => m.IsActive).ToList();
    }
}
