using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Assetra.Services;
using BCrypt.Net;

namespace Assetra.Controllers
{
    public class AccountController : Controller
    {
        private readonly IFirestoreService _firestoreService;
        public AccountController(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                var role = HttpContext.Session.GetString("Role");
                if (role == "User")
                {
                    return RedirectToAction("Index", "UserPortal");
                }
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            username = username.Trim();

            var user = await _firestoreService.GetUserByUsernameAsync(username);
            if (user != null && !string.IsNullOrEmpty(user.PasswordHash) && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("FullName", user.FullName);
                HttpContext.Session.SetString("Role", user.Role);
                
                if (user.Role == "User")
                {
                    return RedirectToAction("Index", "UserPortal");
                }
                return RedirectToAction("Index", "Dashboard");
            }
            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
