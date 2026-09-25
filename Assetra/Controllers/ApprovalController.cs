using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Assetra.Models;
using Assetra.Services;
using Assetra.Helpers;

namespace Assetra.Controllers
{
    public class ApprovalController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public ApprovalController(IFirestoreService firestoreService)
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

            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["Error"] = "Access Denied: You do not have permissions to approve loan requests.";
                context.Result = RedirectToAction("Index", "Dashboard");
                return;
            }

            base.OnActionExecuting(context);
        }

        // List pending approvals
        public async Task<IActionResult> Index()
        {
            var records = await _firestoreService.GetLendingRecordsAsync();
            var pendingRecords = records
                .Where(l => l.Status == "Pending")
                .OrderBy(l => l.DateBorrowed)
                .ToList();

            var today = DateTime.Today;
            var reports = await _firestoreService.GetConditionReportsAsync();
            var overdueReports = reports
                .Where(r => r.IsScheduled && r.DateSubmitted == null && r.ScheduledDate < today)
                .ToList();

            var flaggedBorrowers = new Dictionary<int, int>();
            foreach (var r in overdueReports)
            {
                if (r.Lending?.BorrowedBy.HasValue == true)
                {
                    int uid = r.Lending.BorrowedBy.Value;
                    if (flaggedBorrowers.ContainsKey(uid))
                        flaggedBorrowers[uid]++;
                    else
                        flaggedBorrowers[uid] = 1;
                }
            }

            ViewBag.FlaggedBorrowers = flaggedBorrowers;
            return View(pendingRecords);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var record = await _firestoreService.GetLendingRecordByIdAsync(id);
            if (record != null && record.Status == "Pending")
            {
                record.Status = "Approved";
                record.DateBorrowed = DateTime.Now;

                if (!string.IsNullOrEmpty(record.PropertyId))
                {
                    var property = await _firestoreService.GetPropertyByIdAsync(record.PropertyId);
                    if (property != null)
                    {
                        var lendings = await _firestoreService.GetLendingRecordsAsync();
                        int activeBorrows = lendings
                            .Count(l => l.PropertyId == record.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"));

                        if (activeBorrows + 1 >= property.Quantity)
                        {
                            property.Status = "On Loan";
                        }
                        else
                        {
                            property.Status = "Available";
                        }
                        await _firestoreService.UpdatePropertyAsync(property);
                    }
                }

                await _firestoreService.UpdateLendingRecordAsync(record);
                await ReportHelper.GenerateReportScheduleAsync(_firestoreService, record);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var record = await _firestoreService.GetLendingRecordByIdAsync(id);
            if (record != null && record.Status == "Pending")
            {
                record.Status = "Rejected";
                await _firestoreService.UpdateLendingRecordAsync(record);
            }
            return RedirectToAction("Index");
        }
    }
}
