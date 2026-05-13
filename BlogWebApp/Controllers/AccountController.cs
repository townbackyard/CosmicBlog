using System.Threading.Tasks;
using BlogWebApp.Models;
using BlogWebApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BlogWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<CosmicBlogUser> _signInManager;
        private readonly UserManager<CosmicBlogUser> _userManager;

        public AccountController(
            SignInManager<CosmicBlogUser> signInManager,
            UserManager<CosmicBlogUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [Route("login")]
        public IActionResult Login() => View(new AccountLoginViewModel());

        [Route("login")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AccountLoginViewModel m, string? returnUrl)
        {
            if (!ModelState.IsValid) return View(m);

            var result = await _signInManager.PasswordSignInAsync(
                m.Email, m.Password, isPersistent: true, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(m);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return Redirect("/");
        }

        [Route("logout")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Redirect("/");
        }

        [Route("admin/account")]
        [Authorize("RequireAdmin")]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [Route("admin/account")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Redirect("/login");

            var result = await _userManager.ChangePasswordAsync(user, m.CurrentPassword, m.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err.Description);
                return View(m);
            }

            await _signInManager.RefreshSignInAsync(user);
            ViewBag.Success = true;
            return View(new ChangePasswordViewModel());
        }
    }
}
