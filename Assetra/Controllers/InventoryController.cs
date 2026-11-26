using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Assetra.Models;
using Assetra.Services;
using QRCoder;

namespace Assetra.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public InventoryController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        // List all properties
        public async Task<IActionResult> Index()
        {
            var properties = await _firestoreService.GetPropertiesAsync();
            return View(properties);
        }

        // Show create form
        public IActionResult Create()
        {
            return View();
        }

        // Save new property
        [HttpPost]
        public async Task<IActionResult> Create(Property property, IFormFile imageFile)
        {
            property.PropertyId = await GeneratePropertyIdAsync();
            property.DateAdded = DateTime.UtcNow;
            property.QrCodePath = GenerateQR(property.PropertyId);
            property.Status = "Available";

            string? uploadedPath = SaveUploadedFile(imageFile);
            property.ImagePath = uploadedPath ?? "/images/placeholder.jpg";

            var history = new ConditionHistory
            {
                PropertyId = property.PropertyId,
                ConditionStatus = property.ConditionStatus,
                Notes = "Initial registration of tool.",
                DateRecorded = DateTime.UtcNow,
                RecordedBy = HttpContext.Session.GetString("FullName") ?? "System"
            };

            await _firestoreService.AddPropertyAsync(property);
            await _firestoreService.AddConditionHistoryAsync(history);

            return RedirectToAction("Index");
        }

        // View property details
        public async Task<IActionResult> Details(string id)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(id);
            if (property == null) return NotFound();

            var histories = await _firestoreService.GetConditionHistoriesAsync();
            ViewBag.ConditionHistory = histories
                .Where(c => c.PropertyId == id)
                .OrderByDescending(c => c.DateRecorded)
                .ToList();

            var reports = await _firestoreService.GetConditionReportsAsync();
            ViewBag.ConditionReports = reports
                .Where(r => r.Lending != null && r.Lending.PropertyId == id && r.DateSubmitted != null)
                .OrderByDescending(r => r.DateSubmitted)
                .ToList();

            return View(property);
        }

        // Edit form
        public async Task<IActionResult> Edit(string id)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(id);
            if (property == null) return NotFound();
            return View(property);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Property property, IFormFile imageFile)
        {
            var existing = await _firestoreService.GetPropertyByIdAsync(property.PropertyId);
            if (existing == null) return NotFound();

            if (existing.ConditionStatus != property.ConditionStatus)
            {
                var history = new ConditionHistory
                {
                    PropertyId = property.PropertyId,
                    ConditionStatus = property.ConditionStatus,
                    Notes = $"Condition updated from '{existing.ConditionStatus}' to '{property.ConditionStatus}' via inventory edit.",
                    DateRecorded = DateTime.UtcNow,
                    RecordedBy = HttpContext.Session.GetString("FullName") ?? "System"
                };
                await _firestoreService.AddConditionHistoryAsync(history);
            }

            string? uploadedPath = SaveUploadedFile(imageFile);
            if (uploadedPath != null)
            {
                if (!string.IsNullOrEmpty(existing.ImagePath) && existing.ImagePath.StartsWith("/images/uploads/"))
                {
                    string oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existing.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        try { System.IO.File.Delete(oldPath); } catch { }
                    }
                }
                existing.ImagePath = uploadedPath;
            }

            var maintenanceRecords = await _firestoreService.GetMaintenanceRecordsAsync();
            bool hasActiveMaintenance = maintenanceRecords.Any(m => m.PropertyId == property.PropertyId && m.Status != "Completed");
            if (hasActiveMaintenance)
            {
                existing.Status = "Maintenance";
            }
            else
            {
                existing.Status = property.Status;
            }

            existing.Name = property.Name;
            existing.Category = property.Category;
            existing.Description = property.Description;
            existing.Quantity = property.Quantity;
            existing.Location = property.Location;
            existing.ConditionStatus = property.ConditionStatus;

            await _firestoreService.UpdatePropertyAsync(existing);
            return RedirectToAction("Index");
        }

        // Disable / Enable Tool
        public async Task<IActionResult> Disable(string id)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(id);
            if (property != null)
            {
                if (property.Status == "Disabled")
                {
                    property.Status = "Available";
                    await _firestoreService.UpdatePropertyAsync(property);
                    TempData["Success"] = $"Tool '{property.Name}' has been enabled and is now Available.";
                }
                else
                {
                    var lendings = await _firestoreService.GetLendingRecordsAsync();
                    bool hasActiveLoans = lendings.Any(l => l.PropertyId == property.PropertyId && 
                        (l.Status == "Borrowed" || l.Status == "Approved" || l.Status == "Overdue" || l.Status == "Pending Return" || l.Status == "Immediate Return"));

                    property.Status = "Disabled";
                    await _firestoreService.UpdatePropertyAsync(property);

                    if (hasActiveLoans)
                    {
                        TempData["Success"] = $"Tool '{property.Name}' has been disabled for new borrowings. Existing active loans must still be returned.";
                    }
                    else
                    {
                        TempData["Success"] = $"Tool '{property.Name}' has been disabled.";
                    }
                }
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("/Inventory/Details", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Details", new { id });
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(string id) => await Disable(id);

        // Helper to generate a printable QR Code action
        public async Task<IActionResult> PrintQR(string id)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(id);
            if (property == null) return NotFound();

            // If QrCodePath is a stale file path (not a data URI), regenerate it on the fly
            if (!string.IsNullOrEmpty(property.QrCodePath) && !property.QrCodePath.StartsWith("data:"))
            {
                property.QrCodePath = GenerateQR(property.PropertyId);
                await _firestoreService.UpdatePropertyAsync(property);
            }

            return View(property);
        }

        // Regenerate and persist QR code for an existing asset (fixes stale disk paths)
        public async Task<IActionResult> RegenerateQR(string id)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(id);
            if (property == null) return NotFound();

            property.QrCodePath = GenerateQR(property.PropertyId);
            await _firestoreService.UpdatePropertyAsync(property);

            TempData["Success"] = $"QR code for '{property.Name}' has been regenerated.";
            return RedirectToAction("Details", new { id });
        }

        // --- Private helper methods ---

        private async Task<string> GeneratePropertyIdAsync()
        {
            int maxIdNum = 0;
            var properties = await _firestoreService.GetPropertiesAsync();
            var existingIds = properties.Select(p => p.PropertyId).ToList();
            foreach (var id in existingIds)
            {
                if (id.StartsWith("INV-") && id.Length > 4 && int.TryParse(id.Substring(4), out int num))
                {
                    if (num > maxIdNum)
                    {
                        maxIdNum = num;
                    }
                }
            }
            int nextId = maxIdNum + 1;
            return $"INV-{nextId:D4}";
        }

        private string GenerateQR(string propertyId)
        {
            using var qrGenerator = new QRCodeGenerator();
            var data = qrGenerator.CreateQrCode(propertyId, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(data);
            byte[] bytes = pngQr.GetGraphic(20);

            // Store as Base64 data URI instead of writing to disk.
            // Render's free tier has an ephemeral filesystem — files written to wwwroot
            // are lost on every restart/sleep. Data URIs survive because they live in Firestore.
            string base64 = Convert.ToBase64String(bytes);
            return $"data:image/png;base64,{base64}";
        }

        private string? SaveUploadedFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "uploads");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            return $"/images/uploads/{fileName}";
        }
    }
}
