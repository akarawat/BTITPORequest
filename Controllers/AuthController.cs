using BTITPORequest.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration config, ILogger<AuthController> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ── Redirect to SSO ───────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public IActionResult SsoRedirect()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            var ssoUrl = _config["AppSettings:AuthenUrl"] ?? "";
            if (string.IsNullOrEmpty(ssoUrl))
                return Content("AuthenUrl is not configured.", "text/plain");

            return Redirect(ssoUrl);
        }

        // ── Logout ────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            SessionHelper.ClearUser(HttpContext.Session);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var ssoUrl = _config["AppSettings:AuthenUrl"] ?? "/";
            return Redirect(ssoUrl); // กลับ SSO เพื่อ clear session
        }

        // ── Access Denied ─────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
