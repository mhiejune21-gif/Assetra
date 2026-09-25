using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Assetra.Services;

namespace Assetra.Controllers
{
    public class UserPortalController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public UserPortalController(IFirestoreService firestoreService)
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

        public async Task<IActionResult> Index()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Logout", "Account");
            }

            var properties = await _firestoreService.GetPropertiesAsync();
            var availableItems = properties
                .Where(p => p.Status == "Available" && p.ConditionStatus != "Damaged")
                .ToList();

            var categorizedItems = availableItems
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.CategorizedItems = categorizedItems;

            var lendings = await _firestoreService.GetLendingRecordsAsync();

            var pendingRequests = lendings
                .Where(l => l.BorrowedBy == userId && l.Status == "Pending")
                .OrderByDescending(l => l.DateBorrowed)
                .ToList();

            ViewBag.PendingRequests = pendingRequests;

            var activeLoans = lendings
                .Where(l => l.BorrowedBy == userId && (l.Status == "Borrowed" || l.Status == "Approved"))
                .OrderByDescending(l => l.DateBorrowed)
                .ToList();

            ViewBag.ActiveLoans = activeLoans;

            var activeLendingIds = activeLoans.Select(l => l.LendingId).ToList();
            var reports = await _firestoreService.GetConditionReportsAsync();
            var conditionReports = reports
                .Where(r => activeLendingIds.Contains(r.LendingId))
                .OrderBy(r => r.ScheduledDate)
                .ToList();
            ViewBag.ConditionReports = conditionReports;

            var returnedHistory = lendings
                .Where(l => l.BorrowedBy == userId && l.Status == "Returned")
                .OrderByDescending(l => l.DateReturned)
                .ToList();

            ViewBag.ReturnedHistory = returnedHistory;

            return View();
        }
    }
}
