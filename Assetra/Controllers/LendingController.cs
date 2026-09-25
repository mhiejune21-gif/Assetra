using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Assetra.Models;
using Assetra.Services;
using Assetra.Helpers;

namespace Assetra.Controllers
{
    public class LendingController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public LendingController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = RedirectToAction("Login", "Account");
                return;
            }
            base.OnActionExecuting(context);
        }

        // List lending records with Search, Filter, and Sort
        public async Task<IActionResult> Index(string? search, string? status, string? category, string? sort)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("Role");
            var today = DateTime.Today;

            var allRecords = await _firestoreService.GetLendingRecordsAsync();
            IEnumerable<LendingRecord> query = allRecords;

            if (role != "Admin")
            {
                if (int.TryParse(userIdString, out int userId))
                {
                    query = query.Where(l => l.BorrowedBy == userId);
                }
            }

            // Search by student name, student ID, tool name, tool ID
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(l => 
                    (l.BorrowerName != null && l.BorrowerName.Contains(search, StringComparison.OrdinalIgnoreCase)) || 
                    (l.StudentId != null && l.StudentId.Contains(search, StringComparison.OrdinalIgnoreCase)) || 
                    (l.Property != null && l.Property.Name != null && l.Property.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) || 
                    (l.PropertyId != null && l.PropertyId.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Filter Status
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Overdue")
                {
                    query = query.Where(l => l.Status != "Returned" && l.Status != "Rejected" && l.DueDate < today);
                }
                else
                {
                    query = query.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Filter Category
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(l => l.Property != null && l.Property.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            // Sorting
            query = sort switch
            {
                "date_desc" => query.OrderByDescending(l => l.DateBorrowed),
                "date_asc" => query.OrderBy(l => l.DateBorrowed),
                "due_desc" => query.OrderByDescending(l => l.DueDate),
                "due_asc" => query.OrderBy(l => l.DueDate),
                "return_desc" => query.OrderByDescending(l => l.DateReturned),
                "return_asc" => query.OrderBy(l => l.DateReturned),
                "tool_asc" => query.OrderBy(l => l.Property != null ? l.Property.Name : ""),
                "tool_desc" => query.OrderByDescending(l => l.Property != null ? l.Property.Name : ""),
                "student_asc" => query.OrderBy(l => l.BorrowerName),
                "student_desc" => query.OrderByDescending(l => l.BorrowerName),
                _ => query.OrderByDescending(l => l.DateBorrowed)
            };

            var records = query.ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Category = category;
            ViewBag.Sort = sort;

            var reports = await _firestoreService.GetConditionReportsAsync();
            var overdueLendingIds = reports
                .Where(r => r.IsScheduled && r.DateSubmitted == null && r.ScheduledDate < today)
                .Select(r => r.LendingId)
                .Distinct()
                .ToHashSet();

            ViewBag.OverdueLendingIds = overdueLendingIds;

            return View(records);
        }

        // Show Scan QR Page
        public IActionResult Scan()
        {
            return View();
        }

        // Identify tool scanned via QR code
        public async Task<IActionResult> IdentifyTool(string propertyId)
        {
            var role = HttpContext.Session.GetString("Role");

            if (string.IsNullOrEmpty(propertyId))
            {
                TempData["Error"] = "Invalid QR Scan. Please try again.";
                return RedirectToAction("Scan");
            }

            propertyId = propertyId.Trim();

            var property = await _firestoreService.GetPropertyByIdAsync(propertyId);

            if (property == null && (propertyId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                     propertyId.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var uri = new Uri(propertyId);
                    string lastSegment = uri.Segments.Last().Trim('/');
                    if (!string.IsNullOrEmpty(lastSegment))
                    {
                        var altProperty = await _firestoreService.GetPropertyByIdAsync(lastSegment);
                        if (altProperty != null)
                        {
                            property = altProperty;
                            propertyId = lastSegment;
                        }
                    }
                }
                catch { }
            }

            if (property == null)
            {
                TempData["Error"] = $"Tool with ID '{propertyId}' not found.";
                return RedirectToAction("Scan");
            }

            var lendings = await _firestoreService.GetLendingRecordsAsync();
            int activeBorrows = lendings.Count(l => l.PropertyId == property.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"));
            bool isAvailable = property.Status != "Maintenance" && property.ConditionStatus != "Damaged" && activeBorrows < property.Quantity;

            if (role != "Admin")
            {
                if (isAvailable)
                {
                    return RedirectToAction("Create", new { propertyId = property.PropertyId });
                }
                else
                {
                    if (property.ConditionStatus == "Damaged")
                    {
                        TempData["Error"] = $"Tool '{property.Name}' is marked Damaged and cannot be borrowed.";
                    }
                    else if (property.Status == "Maintenance")
                    {
                        TempData["Error"] = $"Tool '{property.Name}' is currently in maintenance and cannot be borrowed.";
                    }
                    else
                    {
                        TempData["Error"] = $"All units of '{property.Name}' are currently On Loan and cannot be requested for borrowing. Please return the tool to a teacher to process returning.";
                    }
                    return RedirectToAction("Scan");
                }
            }
            else
            {
                if (property.Status == "Maintenance")
                {
                    TempData["Error"] = $"Tool '{property.Name}' is currently in maintenance.";
                    return RedirectToAction("Scan");
                }

                var activeLendings = lendings
                    .Where(l => l.PropertyId == property.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"))
                    .ToList();

                if (activeLendings.Count == 0)
                {
                    if (property.ConditionStatus == "Damaged")
                    {
                        TempData["Error"] = $"Tool '{property.Name}' is marked Damaged and cannot be borrowed.";
                        return RedirectToAction("Scan");
                    }
                    return RedirectToAction("Create", new { propertyId = property.PropertyId });
                }
                else
                {
                    if (isAvailable)
                    {
                        if (activeLendings.Count == 1)
                        {
                            return RedirectToAction("ReturnTool", new { id = activeLendings[0].LendingId });
                        }
                        else
                        {
                            TempData["Success"] = $"Multiple active loans found for '{property.Name}'. Select the student returning the tool below.";
                            return RedirectToAction("Index", new { search = property.PropertyId, status = "Approved" });
                        }
                    }
                    else
                    {
                        if (activeLendings.Count == 1)
                        {
                            return RedirectToAction("ReturnTool", new { id = activeLendings[0].LendingId });
                        }
                        else
                        {
                            return RedirectToAction("Index", new { search = property.PropertyId, status = "Approved" });
                        }
                    }
                }
            }
        }

        // Show create form (Borrow page)
        public async Task<IActionResult> Create(string? propertyId)
        {
            var role = HttpContext.Session.GetString("Role");
            ViewBag.SelectedPropertyId = propertyId;
            
            var allProperties = await _firestoreService.GetPropertiesAsync();
            var lendings = await _firestoreService.GetLendingRecordsAsync();

            var filtered = allProperties.Where(p => p.Status != "Maintenance" && p.ConditionStatus != "Damaged").ToList();
            var availableProperties = filtered.Where(p => {
                int activeCount = lendings.Count(l => l.PropertyId == p.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"));
                return activeCount < p.Quantity;
            }).ToList();

            ViewBag.Properties = availableProperties;
            
            var users = await _firestoreService.GetUsersAsync();
            ViewBag.Users = users.Where(u => u.Role == "User").ToList();

            if (role != "Admin")
            {
                var userIdString = HttpContext.Session.GetString("UserId");
                if (int.TryParse(userIdString, out int userId))
                {
                    var student = await _firestoreService.GetUserByIdAsync(userId);
                    ViewBag.StudentInfo = student;
                }
            }

            return View();
        }

        // Process borrowing transaction
        [HttpPost]
        public async Task<IActionResult> Create(LendingRecord record)
        {
            var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
            if (property == null || property.Status == "Maintenance" || property.ConditionStatus == "Damaged")
            {
                TempData["Error"] = "Selected tool is not available for borrowing.";
                return RedirectToAction("Index");
            }

            var role = HttpContext.Session.GetString("Role");
            var currentUserIdString = HttpContext.Session.GetString("UserId");

            var lendings = await _firestoreService.GetLendingRecordsAsync();
            int activeBorrowsCount = lendings
                .Count(l => l.PropertyId == property.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"));

            if (activeBorrowsCount >= property.Quantity)
            {
                TempData["Error"] = "All units of this tool are currently borrowed.";
                return RedirectToAction("Index");
            }

            if (role == "Admin")
            {
                if (record.BorrowedBy.HasValue)
                {
                    var studentUser = await _firestoreService.GetUserByIdAsync(record.BorrowedBy.Value);
                    if (studentUser != null)
                    {
                        record.BorrowerName = studentUser.FullName;
                        record.StudentId = studentUser.Username;
                    }
                }
                else if (string.IsNullOrWhiteSpace(record.BorrowerName) || string.IsNullOrWhiteSpace(record.StudentId))
                {
                    TempData["Error"] = "Borrower Name and Student ID are required.";
                    return RedirectToAction("Create", new { propertyId = record.PropertyId });
                }

                record.Status = "Borrowed";
                record.DateBorrowed = DateTime.Now;
                record.BorrowedCondition = property.ConditionStatus;
                record.ProcessedBy = HttpContext.Session.GetString("FullName") ?? "Teacher";
                
                if (activeBorrowsCount + 1 >= property.Quantity)
                {
                    property.Status = "On Loan";
                }
                else
                {
                    property.Status = "Available";
                }
                await _firestoreService.UpdatePropertyAsync(property);
            }
            else
            {
                if (int.TryParse(currentUserIdString, out int studentId))
                {
                    var studentUser = await _firestoreService.GetUserByIdAsync(studentId);
                    if (studentUser != null)
                    {
                        record.BorrowedBy = studentUser.UserId;
                        record.BorrowerName = studentUser.FullName;
                        record.StudentId = studentUser.Username;
                    }
                }
                record.Status = "Pending";
                record.DateBorrowed = DateTime.Now;
                record.BorrowedCondition = property.ConditionStatus;
            }

            await _firestoreService.AddLendingRecordAsync(record);

            if (record.Status == "Borrowed")
            {
                await ReportHelper.GenerateReportScheduleAsync(_firestoreService, record);
            }

            TempData["Success"] = role == "Admin" 
                ? $"Tool '{property.Name}' successfully borrowed by {record.BorrowerName}."
                : $"Borrow request submitted for '{property.Name}'. Pending teacher approval.";

            return RedirectToAction("Index");
        }

        // Show Return Confirmation Page
        public async Task<IActionResult> ReturnTool(int id)
        {
            var record = await _firestoreService.GetLendingRecordByIdAsync(id);
            if (record == null) return NotFound();
            
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: Only teachers/admins can process returns.";
                return RedirectToAction("Index");
            }

            return View(record);
        }

        // Mark as returned
        [HttpPost]
        public async Task<IActionResult> ReturnTool(int id, string returnedCondition, string? notes)
        {
            var record = await _firestoreService.GetLendingRecordByIdAsync(id);
            if (record == null) return NotFound();

            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: Only teachers/admins can process returns.";
                return RedirectToAction("Index");
            }

            if (record.Status == "Borrowed" || record.Status == "Approved" || record.Status == "Overdue" || record.Status == "Pending")
            {
                record.Status = "Returned";
                record.DateReturned = DateTime.Now;
                record.ReturnedCondition = returnedCondition;
                record.ProcessedBy = HttpContext.Session.GetString("FullName") ?? "Teacher";

                if (!string.IsNullOrEmpty(record.PropertyId))
                {
                    var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
                    if (property != null)
                    {
                        property.Status = "Available";
                        property.ConditionStatus = returnedCondition;
                        await _firestoreService.UpdatePropertyAsync(property);
                    }

                    var history = new ConditionHistory
                    {
                        PropertyId = record.PropertyId,
                        ConditionStatus = returnedCondition,
                        Notes = string.IsNullOrWhiteSpace(notes) ? $"Returned by student {record.BorrowerName}." : notes,
                        DateRecorded = DateTime.Now,
                        RecordedBy = HttpContext.Session.GetString("FullName") ?? "Teacher"
                    };
                    await _firestoreService.AddConditionHistoryAsync(history);
                }

                await _firestoreService.UpdateLendingRecordAsync(record);
                TempData["Success"] = $"Tool '{record.Property?.Name}' successfully returned and verified.";
            }
            else
            {
                TempData["Error"] = "This borrowing record is not active or has already been returned.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Return(int id)
        {
            return RedirectToAction("ReturnTool", new { id = id });
        }
    }
}
