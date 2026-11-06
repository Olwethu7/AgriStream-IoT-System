using AgriStreamWeb.Data;
using AgriStreamWeb.Models;
using AgriStreamWeb.Services;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStreamWeb.Controllers;

public class DashboardController : Controller
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff" };

    private readonly BlobServiceClient _blobService;
    private readonly FunctionApiClient _functions;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(BlobServiceClient blobService, FunctionApiClient functions, AppDbContext db,
        IConfiguration config, ILogger<DashboardController> logger)
    {
        _blobService = blobService;
        _functions = functions;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public IActionResult Index() => RedirectToAction("Index", "Home");

    /// <summary>Uploads a drone image to the drone-images container. The BlobTriggerFunction picks it up.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile? image, string? fieldId,
        double? soilMoisture, double? temperature, double? humidity)
    {
        var maxMb = _config.GetValue("BlobStorage:MaxUploadMegabytes", 10);

        if (image == null || image.Length == 0)
            return Fail("Please choose an image to upload.");

        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Fail("Only JPG, PNG, WEBP or TIFF images are allowed.");

        if (image.Length > maxMb * 1024L * 1024L)
            return Fail($"Image is larger than {maxMb} MB.");

        var field = string.IsNullOrWhiteSpace(fieldId) ? "FIELD-01" : fieldId.Trim().ToUpperInvariant();

        try
        {
            var container = _blobService.GetBlobContainerClient(_config["BlobStorage:ContainerName"] ?? "drone-images");
            await container.CreateIfNotExistsAsync();

            // Unique, safe name: field/yyyyMMdd-HHmmss-guid.ext
            var blobName = $"{field}/{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{ext}";
            var blob = container.GetBlobClient(blobName);

            // Sensor readings travel as blob metadata so the function can log them.
            var metadata = new Dictionary<string, string>
            {
                ["fieldId"] = field,
                ["uploadedBy"] = User.Identity?.Name ?? "unknown"
            };
            if (soilMoisture.HasValue) metadata["soilMoisture"] = soilMoisture.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (temperature.HasValue) metadata["temperature"] = temperature.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (humidity.HasValue) metadata["humidity"] = humidity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            await using var stream = image.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = image.ContentType },
                Metadata = metadata
            });

            TempData["Success"] = $"Uploaded {image.FileName} for {field}. Telemetry will appear shortly.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob upload failed.");
            return Fail("Upload failed. Check the Azure Storage connection string.");
        }

        return RedirectToAction("Index", "Home");
    }

    /// <summary>JSON proxy to HttpGetTelemetry, used by the dashboard's live polling.</summary>
    [HttpGet]
    public async Task<IActionResult> Telemetry(int top = 20, string? fieldId = null)
    {
        try
        {
            var data = await _functions.GetTelemetryAsync(Math.Clamp(top, 1, 100), fieldId, HttpContext.RequestAborted);
            return Json(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telemetry fetch failed.");
            return StatusCode(502, new { error = "Telemetry service unavailable" });
        }
    }

    /// <summary>JSON proxy to HttpPostIrrigation.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Irrigate([FromForm] IrrigationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Enter a field ID and a duration between 1 and 240 minutes." });

        try
        {
            var response = await _functions.SendIrrigationCommandAsync(
                request.FieldId.Trim().ToUpperInvariant(), request.DurationMinutes,
                User.Identity?.Name ?? "unknown", HttpContext.RequestAborted);

            var body = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = string.IsNullOrWhiteSpace(body) ? "{}" : body
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Irrigation command failed.");
            return StatusCode(502, new { error = "Irrigation service unavailable" });
        }
    }

    /// <summary>Current user's status (updated by the QueueTriggerFunction).</summary>
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var username = User.Identity?.Name;
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => new { u.Status, u.LastIrrigationAt })
            .FirstOrDefaultAsync();

        return user == null ? NotFound() : Json(user);
    }

    private IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Index", "Home");
    }
}
