using BTITPORequest.Helpers;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IPOService _poService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IPOService poService, ILogger<DashboardController> logger)
        {
            _poService = poService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            // #Debug Skip authentication for development *Edit authen at AuthService.BuildSessionFromSsoAsync() to return fixed user*
            // var user = new UserSessionModel
            // {
            //     SsoId = "1234679",
            //     SamAcc = "jirawat.k",
            //     EmpCode = "S01409",
            //     FullName = "jirawat kanfan",
            //     Email = "jirawat.k@berninathailand.com"
            //};
            var user = SessionHelper.GetUserFromClaims(User);
            if (user == null) return RedirectToAction("Login", "Auth");

            var from = dateFrom ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = dateTo ?? DateTime.Today;

            var vm = await _poService.GetDashboardDataAsync(user.SamAcc, user.Role == "Admin", from, to);
            vm.CurrentUserSam = user.SamAcc;
            vm.CurrentUserName = user.FullName;

            return View(vm);
        }
    }
}
