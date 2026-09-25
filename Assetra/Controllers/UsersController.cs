using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Assetra.Models;
using Assetra.Services;
using BCrypt.Net;

namespace Assetra.Controllers
{
    public class UsersController : Controller
    {
        private readonly IFirestoreService _firestoreService;

        public UsersController(IFirestoreService firestoreService)
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
                TempData["Error"] = "Access Denied: You do not have permissions to manage users.";
                context.Result = RedirectToAction("Index", "Dashboard");
                return;
            }

            base.OnActionExecuting(context);
        }

        // List all users
        public async Task<IActionResult> Index()
        {
            var users = await _firestoreService.GetUsersAsync();

            var today = DateTime.Today;
            var allReports = await _firestoreService.GetConditionReportsAsync();
            var overdueReports = allReports
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
            return View(users);
        }

        // Create form
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user, string password)
        {
            var existing = await _firestoreService.GetUserByUsernameAsync(user.Username);
            if (existing != null)
            {
                ViewBag.Error = "Username is already taken.";
                return View(user);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                return View(user);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.CreatedAt = DateTime.UtcNow;

            await _firestoreService.AddUserAsync(user);

            return RedirectToAction("Index");
        }

        // Bulk CSV Import Action
        [HttpPost]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file.";
                return RedirectToAction("Index");
            }

            try
            {
                int importedCount = 0;
                int skippedCount = 0;

                using (var reader = new StreamReader(csvFile.OpenReadStream()))
                {
                    string? headerLine = reader.ReadLine();
                    if (headerLine == null)
                    {
                        TempData["Error"] = "The CSV file is empty.";
                        return RedirectToAction("Index");
                    }

                    var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();
                    int usernameIdx = headers.IndexOf("username");
                    int fullNameIdx = headers.IndexOf("fullname");
                    int roleIdx = headers.IndexOf("role");
                    int passIdx = headers.IndexOf("password");

                    if (usernameIdx == -1) usernameIdx = 0;
                    if (fullNameIdx == -1) fullNameIdx = 1;
                    if (roleIdx == -1 && headers.Count > 2) roleIdx = 2;
                    if (passIdx == -1 && headers.Count > 3) passIdx = 3;

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = line.Split(',');
                        if (values.Length <= Math.Max(usernameIdx, fullNameIdx))
                        {
                            skippedCount++;
                            continue;
                        }

                        string username = values[usernameIdx].Trim();
                        string fullName = values[fullNameIdx].Trim();

                        string role = "User";
                        if (roleIdx != -1 && values.Length > roleIdx)
                        {
                            string rawRole = values[roleIdx].Trim();
                            if (!string.IsNullOrWhiteSpace(rawRole))
                            {
                                role = rawRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";
                            }
                        }

                        string userPassword = username; // Default to Username as password
                        if (passIdx != -1 && values.Length > passIdx)
                        {
                            string rawPass = values[passIdx].Trim();
                            if (!string.IsNullOrWhiteSpace(rawPass))
                            {
                                userPassword = rawPass;
                            }
                        }

                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName))
                        {
                            skippedCount++;
                            continue;
                        }

                        var existingUser = await _firestoreService.GetUserByUsernameAsync(username);
                        if (existingUser != null)
                        {
                            skippedCount++;
                            continue;
                        }

                        var newUser = new User
                        {
                            Username = username,
                            FullName = fullName,
                            Role = role,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(userPassword),
                            CreatedAt = DateTime.UtcNow
                        };

                        await _firestoreService.AddUserAsync(newUser);
                        importedCount++;
                    }
                }

                TempData["Success"] = $"Successfully imported {importedCount} student accounts. {skippedCount} rows were skipped (duplicates or invalid rows).";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error importing accounts: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Edit form
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _firestoreService.GetUserByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(User user, string? newPassword)
        {
            var existingUser = await _firestoreService.GetUserByIdAsync(user.UserId);
            if (existingUser == null) return NotFound();

            if (existingUser.Username != user.Username)
            {
                var checkUser = await _firestoreService.GetUserByUsernameAsync(user.Username);
                if (checkUser != null)
                {
                    ViewBag.Error = "Username is already taken.";
                    return View(user);
                }
            }

            existingUser.Username = user.Username;
            existingUser.FullName = user.FullName;
            existingUser.Role = user.Role;

            if (!string.IsNullOrEmpty(newPassword))
            {
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }

            await _firestoreService.UpdateUserAsync(existingUser);
            return RedirectToAction("Index");
        }

        // Delete user
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _firestoreService.GetUserByIdAsync(id);
            if (user != null)
            {
                var currentUserId = HttpContext.Session.GetString("UserId");
                if (currentUserId == id.ToString())
                {
                    TempData["Error"] = "You cannot delete your own account.";
                    return RedirectToAction("Index");
                }

                await _firestoreService.DeleteUserAsync(id);
            }
            return RedirectToAction("Index");
        }
    }
}
