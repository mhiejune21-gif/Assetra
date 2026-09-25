using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Assetra.Models;
using Assetra.Services;

namespace Assetra.Controllers
{
    public class ConditionMonitoringController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public ConditionMonitoringController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        // --- Student Actions ---

        // Show submission form
        public async Task<IActionResult> SubmitReport(int lendingId, int? reportId)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var lending = await _firestoreService.GetLendingRecordByIdAsync(lendingId);
            if (lending == null) return NotFound();

            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && int.TryParse(userIdString, out int userId))
            {
                if (lending.BorrowedBy != userId)
                {
                    TempData["Error"] = "Access Denied: You do not own this borrowing record.";
                    return RedirectToAction("Index", "UserPortal");
                }
            }

            ViewBag.Lending = lending;
            ViewBag.IsEmergency = (reportId == null);

            if (reportId == null)
            {
                int borrowDays = (lending.DueDate.Date - lending.DateBorrowed.Date).Days;
                if (borrowDays < 1) borrowDays = 1;

                int maxIncidents = borrowDays <= 7 ? 2 : (borrowDays <= 14 ? 4 : (borrowDays <= 30 ? 6 : 10));
                
                var allReports = await _firestoreService.GetConditionReportsAsync();
                int currentIncidents = allReports
                    .Count(r => r.LendingId == lendingId && !r.IsScheduled && r.DateSubmitted != null);

                if (currentIncidents >= maxIncidents)
                {
                    TempData["Error"] = $"Access Denied: You have reached the limit of {maxIncidents} incident reports for this borrowing period.";
                    return RedirectToAction("Index", "UserPortal");
                }
            }

            if (reportId.HasValue)
            {
                var report = await _firestoreService.GetConditionReportByIdAsync(reportId.Value);
                if (report == null) return NotFound();
                return View(report);
            }

            return View(new ConditionReport { LendingId = lendingId });
        }

        // Process report submission
        [HttpPost]
        public async Task<IActionResult> SubmitReport(int lendingId, int? reportId, string condition, string remarks, IFormFile photoFile)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var lending = await _firestoreService.GetLendingRecordByIdAsync(lendingId);
            if (lending == null) return NotFound();

            if (reportId == null)
            {
                int borrowDays = (lending.DueDate.Date - lending.DateBorrowed.Date).Days;
                if (borrowDays < 1) borrowDays = 1;

                int maxIncidents = borrowDays <= 7 ? 2 : (borrowDays <= 14 ? 4 : (borrowDays <= 30 ? 6 : 10));
                
                var allReports = await _firestoreService.GetConditionReportsAsync();
                int currentIncidents = allReports
                    .Count(r => r.LendingId == lendingId && !r.IsScheduled && r.DateSubmitted != null);

                if (currentIncidents >= maxIncidents)
                {
                    TempData["Error"] = $"Access Denied: You have reached the limit of {maxIncidents} incident reports for this borrowing period.";
                    return RedirectToAction("Index", "UserPortal");
                }
            }

            string? photoPath = null;
            if (photoFile != null && photoFile.Length > 0)
            {
                try
                {
                    string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "reports");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(photoFile.FileName)}";
                    string filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        photoFile.CopyTo(stream);
                    }
                    photoPath = $"/images/reports/{fileName}";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error saving uploaded file: {ex.Message}";
                    return RedirectToAction("SubmitReport", new { lendingId = lendingId, reportId = reportId });
                }
            }

            ConditionReport report;
            bool isNew = false;

            if (reportId.HasValue)
            {
                var existing = await _firestoreService.GetConditionReportByIdAsync(reportId.Value);
                if (existing == null) return NotFound();
                report = existing;
            }
            else
            {
                report = new ConditionReport
                {
                    LendingId = lendingId,
                    IsScheduled = false,
                };
                isNew = true;
            }

            report.DateSubmitted = DateTime.Now;
            report.Condition = condition;
            report.Remarks = remarks;
            if (photoPath != null)
            {
                report.PhotoPath = photoPath;
            }

            if (condition == "Good")
            {
                report.Status = "Approved";
                report.ReviewedBy = "System";
                report.DateReviewed = DateTime.Now;
                report.CustodianRemarks = "Automatically approved by system.";
            }
            else
            {
                report.Status = "Pending";
            }

            if (isNew)
            {
                await _firestoreService.AddConditionReportAsync(report);
            }
            else
            {
                await _firestoreService.UpdateConditionReportAsync(report);
            }

            if (!string.IsNullOrEmpty(lending.PropertyId) && !string.IsNullOrEmpty(condition))
            {
                var property = await _firestoreService.GetPropertyByIdAsync(lending.PropertyId);
                if (property != null)
                {
                    if (condition == "Good")
                        property.ConditionStatus = "Good";
                    else if (condition == "Minor Damage")
                        property.ConditionStatus = "Minor Damage";
                    else if (condition == "Critical Damage" || condition == "For Repair")
                        property.ConditionStatus = "Damaged";

                    await _firestoreService.UpdatePropertyAsync(property);

                    var history = new ConditionHistory
                    {
                        PropertyId = lending.PropertyId,
                        ConditionStatus = property.ConditionStatus,
                        Notes = $"Reported by borrower during monitoring: {remarks}",
                        DateRecorded = DateTime.Now,
                        RecordedBy = HttpContext.Session.GetString("FullName") ?? "Borrower"
                    };
                    await _firestoreService.AddConditionHistoryAsync(history);
                }
            }

            TempData["Success"] = condition == "Good" 
                ? "Condition report submitted and auto-approved." 
                : "Condition report submitted successfully. Awaiting custodian review.";

            var role = HttpContext.Session.GetString("Role");
            return RedirectToAction("Index", role == "Admin" ? "Lending" : "UserPortal");
        }

        // --- Custodian Actions ---

        // Monitoring dashboard
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: Teachers/Admins only.";
                return RedirectToAction("Index", "UserPortal");
            }

            var reports = await _firestoreService.GetConditionReportsAsync();

            var pendingReviews = reports
                .Where(r => r.Status == "Pending" && r.Condition != "Good" && r.DateSubmitted != null)
                .OrderByDescending(r => r.DateSubmitted)
                .ToList();

            var criticalAlerts = reports
                .Where(r => (r.Condition == "Critical Damage" || r.Condition == "For Repair") && r.Status == "Pending")
                .ToList();

            var allSubmitted = reports
                .Where(r => r.DateSubmitted != null)
                .OrderByDescending(r => r.DateSubmitted)
                .ToList();

            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CriticalAlerts = criticalAlerts;

            return View(allSubmitted);
        }

        // Show review form
        public async Task<IActionResult> Review(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: Teachers/Admins only.";
                return RedirectToAction("Index", "UserPortal");
            }

            var report = await _firestoreService.GetConditionReportByIdAsync(id);
            if (report == null) return NotFound();

            var reports = await _firestoreService.GetConditionReportsAsync();
            ViewBag.ToolHistory = reports
                .Where(r => r.Lending != null && report.Lending != null && r.Lending.PropertyId == report.Lending.PropertyId && r.DateSubmitted != null)
                .OrderByDescending(r => r.DateSubmitted)
                .ToList();

            return View(report);
        }

        // Process review submission
        [HttpPost]
        public async Task<IActionResult> Review(int id, string action, string custodianRemarks)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: Teachers/Admins only.";
                return RedirectToAction("Index", "UserPortal");
            }

            var report = await _firestoreService.GetConditionReportByIdAsync(id);
            if (report == null) return NotFound();

            report.ReviewedBy = HttpContext.Session.GetString("FullName") ?? "Custodian";
            report.DateReviewed = DateTime.Now;
            report.CustodianRemarks = custodianRemarks;

            if (action == "Approve")
            {
                report.Status = "Approved";
                TempData["Success"] = "Report approved. Borrower allowed to continue using the asset.";
            }
            else if (action == "RequestReturn")
            {
                report.Status = "ReturnRequested";
                if (report.Lending != null)
                {
                    report.Lending.Status = "Return Requested";
                    await _firestoreService.UpdateLendingRecordAsync(report.Lending);
                }
                TempData["Success"] = "Return requested. Borrower has been notified to return the asset.";
            }
            else if (action == "SendToMaintenance")
            {
                report.Status = "Resolved";
                if (report.Lending != null)
                {
                    report.Lending.Status = "Return Requested";
                    await _firestoreService.UpdateLendingRecordAsync(report.Lending);
                    
                    if (!string.IsNullOrEmpty(report.Lending.PropertyId))
                    {
                        var property = await _firestoreService.GetPropertyByIdAsync(report.Lending.PropertyId);
                        if (property != null)
                        {
                            property.Status = "Maintenance";
                            await _firestoreService.UpdatePropertyAsync(property);
                        }

                        var maintenance = new MaintenanceRecord
                        {
                            PropertyId = report.Lending.PropertyId,
                            Description = $"[Condition Report Alert] Scanned by {report.Lending.BorrowerName}. Remarks: {report.Remarks}",
                            Status = "Scheduled",
                            ScheduledDate = DateTime.Now
                        };
                        await _firestoreService.AddMaintenanceRecordAsync(maintenance);

                        var history = new ConditionHistory
                        {
                            PropertyId = report.Lending.PropertyId,
                            ConditionStatus = "Damaged",
                            Notes = $"Sent to maintenance via custodian review: {custodianRemarks}",
                            DateRecorded = DateTime.Now,
                            RecordedBy = report.ReviewedBy
                        };
                        await _firestoreService.AddConditionHistoryAsync(history);
                    }
                }
                TempData["Success"] = "Asset routed to Maintenance. Tool is marked unavailable for loans.";
            }

            await _firestoreService.UpdateConditionReportAsync(report);
            return RedirectToAction("Index");
        }
    }
}
