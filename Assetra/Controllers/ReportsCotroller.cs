using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Assetra.Services;

namespace Assetra.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public ReportsController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> InventoryReport(DateTime? startDate, DateTime? endDate)
        {
            var properties = await _firestoreService.GetPropertiesAsync();
            var query = properties.AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(p => p.DateAdded >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(p => p.DateAdded <= endDate.Value);

            var list = query.ToList();

            ViewBag.TotalItems = list.Count;
            ViewBag.Available = list.Count(p => p.Status == "Available");
            ViewBag.OnLoan = list.Count(p => p.Status == "On Loan");
            ViewBag.ForRepair = list.Count(p => p.ConditionStatus == "For Repair");
            ViewBag.Damaged = list.Count(p => p.ConditionStatus == "Damaged");
            ViewBag.Items = list;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View();
        }

        public async Task<IActionResult> LendingReport(DateTime? startDate, DateTime? endDate)
        {
            var lendings = await _firestoreService.GetLendingRecordsAsync();
            var query = lendings.AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(l => l.DateBorrowed >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.DateBorrowed <= endDate.Value);

            var list = query.ToList();

            ViewBag.Records = list;
            ViewBag.TotalBorrowed = list.Count;
            ViewBag.Returned = list.Count(l => l.Status == "Returned");
            ViewBag.Overdue = list.Count(l => l.Status == "Overdue");
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View();
        }
    }
}
