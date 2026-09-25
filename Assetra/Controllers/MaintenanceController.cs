using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Assetra.Models;
using Assetra.Services;

namespace Assetra.Controllers
{
    public class MaintenanceController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public MaintenanceController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task<IActionResult> Index()
        {
            var records = await _firestoreService.GetMaintenanceRecordsAsync();
            return View(records);
        }

        public async Task<IActionResult> Create()
        {
            var properties = await _firestoreService.GetPropertiesAsync();
            ViewBag.Properties = properties;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(MaintenanceRecord record)
        {
            record.Status = "Scheduled";
            
            if (string.IsNullOrWhiteSpace(record.Description))
            {
                record.Description = "Routine maintenance scheduled.";
            }

            if (!string.IsNullOrEmpty(record.PropertyId))
            {
                var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
                if (property != null)
                {
                    property.Status = "Maintenance";
                    await _firestoreService.UpdatePropertyAsync(property);
                }
            }

            await _firestoreService.AddMaintenanceRecordAsync(record);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> MarkInProgress(int id)
        {
            var records = await _firestoreService.GetMaintenanceRecordsAsync();
            var record = records.FirstOrDefault(m => m.MaintenanceId == id);
            if (record != null)
            {
                record.Status = "In Progress";
                await _firestoreService.UpdateMaintenanceRecordAsync(record);

                if (!string.IsNullOrEmpty(record.PropertyId))
                {
                    var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
                    if (property != null)
                    {
                        property.Status = "Maintenance";
                        await _firestoreService.UpdatePropertyAsync(property);
                    }
                }
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> MarkComplete(int id)
        {
            var records = await _firestoreService.GetMaintenanceRecordsAsync();
            var record = records.FirstOrDefault(m => m.MaintenanceId == id);
            if (record != null)
            {
                record.Status = "Completed";
                await _firestoreService.UpdateMaintenanceRecordAsync(record);

                if (!string.IsNullOrEmpty(record.PropertyId))
                {
                    var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
                    if (property != null)
                    {
                        property.Status = "Available";
                        property.ConditionStatus = "Good";
                        await _firestoreService.UpdatePropertyAsync(property);

                        var history = new ConditionHistory
                        {
                            PropertyId = property.PropertyId,
                            ConditionStatus = "Good",
                            Notes = "Restored to Good condition after completing scheduled maintenance.",
                            DateRecorded = DateTime.Now,
                            RecordedBy = HttpContext.Session.GetString("FullName") ?? "System"
                        };
                        await _firestoreService.AddConditionHistoryAsync(history);
                    }
                }
            }
            return RedirectToAction("Index");
        }
    }
}
