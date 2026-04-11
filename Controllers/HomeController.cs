using BTITPORequest.Helpers;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BTITPORequest.Controllers
{
    /// <summary>
    /// Handles the root URL "/" which is the SSO callback endpoint.
    /// btauthen redirects back here with query params:
    /// ?id=GUID&user=DOMAIN\SAM&email=...&fname=...&depart=...
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _config;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAuthService authService, IConfiguration config, ILogger<HomeController> logger)
        {
            _authService = authService;
            _config = config;
            _logger = logger;
        }

        [HttpGet("/")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? id, string? user, string? email, string? fname, string? depart)
        {
            // ── Case 1: SSO callback with query params ────────
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(user))
            {
                return await ProcessSsoCallbackAsync(id, user, email ?? "", fname ?? "", depart ?? "");
            }

            // ── Case 2: Already authenticated → go to Dashboard
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            // ── Case 3: Not authenticated → redirect to SSO ──
            var ssoUrl = _config["AppSettings:AuthenUrl"] ?? "";
            if (string.IsNullOrEmpty(ssoUrl))
                return Content("AuthenUrl not configured. Please check appsettings.json", "text/plain");

            return Redirect(ssoUrl);
        }

        private async Task<IActionResult> ProcessSsoCallbackAsync(
            string ssoId, string adUser, string email, string fname, string depart)
        {
            var session = await _authService.BuildSessionFromSsoAsync(ssoId, adUser, email, fname, depart);
            if (session == null)
            {
                _logger.LogWarning("SSO callback: BuildSession failed for user={user}", adUser);
                var ssoUrl = _config["AppSettings:AuthenUrl"] ?? "/";
                return Redirect(ssoUrl);
            }

            // Build Cookie Claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  session.FullName),
                new(ClaimTypes.Email, session.Email),
                new(ClaimTypes.Role,  session.Role),
                new("sso_id",         session.SsoId),
                new("samacc",         session.SamAcc),
                new("emp_code",       session.EmpCode),
                new("department",     session.Department),
                new("depmgr_sam",     session.DeptManagerSam),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // Store full session in cookie session store
            SessionHelper.SetUser(HttpContext.Session, session);

            _logger.LogInformation("SSO Login success: {sam} ({name}) dept={dept}",
                session.SamAcc, session.FullName, session.Department);

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
