using System.Diagnostics;
using System.Security.Claims;
using AgriStreamWeb.Data;
using AgriStreamWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStreamWeb.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var username = User.Identity?.Name ?? string.Empty;
        var model = new DashboardViewModel
        {
            DisplayName = User.FindFirstValue("FullName") ?? username
        };

        try
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user != null)
            {
                model.UserStatus = user.Status;
                model.LastIrrigationAt = user.LastIrrigationAt;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load user status from SQL.");
            model.UserStatus = "Unknown";
        }

        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
