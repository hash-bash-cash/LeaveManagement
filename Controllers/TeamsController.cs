using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Data;
using LMS.Models;
using LMS.Constants;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class TeamsController(ApplicationDbContext context) : Controller
{
    // GET: Teams
    public async Task<IActionResult> Index()
    {
        var teams = await context.Teams
            .Include(t => t.Department)
            .OrderBy(t => t.Department!.Name)
            .ThenBy(t => t.Name)
            .ToListAsync();
        return View(teams);
    }

    // GET: Teams/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Departments = await context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View();
    }

    // POST: Teams/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Team team)
    {
        if (ModelState.IsValid)
        {
            context.Add(team);
            await context.SaveChangesAsync();
            TempData["Success"] = "Team created successfully.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Departments = await context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(team);
    }

    // GET: Teams/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var team = await context.Teams.FindAsync(id);
        if (team == null) return NotFound();

        ViewBag.Departments = await context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(team);
    }

    // POST: Teams/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Team team)
    {
        if (id != team.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                context.Update(team);
                await context.SaveChangesAsync();
                TempData["Success"] = "Team updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TeamExists(team.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Departments = await context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(team);
    }

    // POST: Teams/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var team = await context.Teams.FindAsync(id);
        if (team == null) return NotFound();

        context.Teams.Remove(team);
        await context.SaveChangesAsync();
        TempData["Success"] = "Team deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private bool TeamExists(int id)
    {
        return context.Teams.Any(e => e.Id == id);
    }
}
