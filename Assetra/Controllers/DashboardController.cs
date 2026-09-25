using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Assetra.Services;

namespace Assetra.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public DashboardController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") == "User")
            {
                return RedirectToAction("Index", "UserPortal");
            }

            var today = DateTime.Today;

            var allProps = await _firestoreService.GetPropertiesAsync();
            var allLendings = await _firestoreService.GetLendingRecordsAsync();

            int totalAvailable = 0;
            int totalBorrowed = 0;
            foreach (var p in allProps)
            {
                if (p.Status == "Maintenance" || p.ConditionStatus == "Damaged")
                {
                    continue;
                }
                int active = allLendings.Count(l => l.PropertyId == p.PropertyId && (l.Status == "Borrowed" || l.Status == "Approved"));
                totalBorrowed += active;
                totalAvailable += Math.Max(0, p.Quantity - active);
            }

            ViewBag.TotalItems = allProps.Sum(p => p.Quantity);
            ViewBag.AvailableItems = totalAvailable;
            ViewBag.BorrowedItems = totalBorrowed;
            ViewBag.PendingRequests = allLendings.Count(l => l.Status == "Pending");
            ViewBag.OverdueItems = allLendings.Count(l => l.Status != "Returned" && l.Status != "Rejected" && l.DueDate < today);

            ViewBag.Good = allProps.Count(p => p.ConditionStatus == "Good");
            ViewBag.MinorDamage = allProps.Count(p => p.ConditionStatus == "Minor Damage");
            ViewBag.Damaged = allProps.Count(p => p.ConditionStatus == "Damaged");
            ViewBag.MissingParts = allProps.Count(p => p.ConditionStatus == "Missing Parts");

            ViewBag.RecentTransactions = allLendings
                .OrderByDescending(l => l.DateBorrowed)
                .Take(6)
                .ToList();

            return View();
        }
    }
}