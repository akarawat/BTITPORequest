using BTITPORequest.Helpers;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BTITPORequest.Controllers
{
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

        // ── Root "/" — SSO Callback entry point ──────────────
        [HttpGet("")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? id, string? user, string? email, string? fname, string? depart)
        {
            // Case 1: SSO callback — มี id + user query params
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(user))
            {
                _logger.LogInformation("SSO Callback received. id={id} user={user}", id, user);
                return await ProcessSsoCallbackAsync(id, user, email ?? "", fname ?? "", depart ?? "");
            }

            // Case 2: Already logged in → Dashboard
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            // Case 3: Not logged in → go to SSO
            return RedirectToSso();
        }

        // ── Process SSO Callback ──────────────────────────────
        private async Task<IActionResult> ProcessSsoCallbackAsync(
            string ssoId, string adUser, string email, string fname, string depart)
        {
            try
            {
                var session = await _authService.BuildSessionFromSsoAsync(
                    ssoId, adUser, email, fname, depart);

                if (session == null)
                {
                    // ไม่ควรเกิดขึ้น (BuildSession ไม่คืน null แล้ว) — แต่ป้องกันไว้
                    _logger.LogError("BuildSessionFromSsoAsync returned null for user={user}", adUser);
                    return View("SsoError", (object)"Session could not be created. Please try again.");
                }

                // สร้าง Cookie Claims — ไม่เก็บ signature image ใน claim (ใหญ่เกิน)
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

                var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8)
                    });

                // เก็บ full session (รวม signature image) ไว้ใน server-side session
                SessionHelper.SetUser(HttpContext.Session, session);

                _logger.LogInformation("Login OK: {sam} ({name})", session.SamAcc, session.FullName);
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSO callback exception for user={user}", adUser);
                return View("SsoError", (object)$"Login error: {ex.Message}");
            }
        }

        // ── Redirect to SSO ───────────────────────────────────
        private IActionResult RedirectToSso()
        {
            var ssoUrl = _config["AppSettings:AuthenUrl"] ?? "";
            if (string.IsNullOrEmpty(ssoUrl))
                return View("SsoError", (object)"AuthenUrl is not configured in appsettings.json");
            return Redirect(ssoUrl);
        }
    }
}
