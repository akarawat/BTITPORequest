using BTITPORequest.Helpers;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTITPORequest.Data;

namespace BTITPORequest.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly DbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AdminController> _logger;
        private readonly IPOService _poService;

        public AdminController(DbContext db, IConfiguration config,
            ILogger<AdminController> logger, IPOService poService)
        {
            _db       = db;
            _config   = config;
            _logger   = logger;
            _poService = poService;
        }

        // ── Only Admin can access ─────────────────────────────
        private bool IsAdmin()
        {
            var user = SessionHelper.GetUser(HttpContext.Session)
                    ?? SessionHelper.GetUserFromClaims(User);
            return user?.Role == "Admin";
        }

        // ── Index: Employee list + role management ────────────
        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? dept)
        {
            if (!IsAdmin()) return Forbid();

            var vm = new AdminIndexViewModel
            {
                SearchText = search,
                FilterDept = dept
            };

            try
            {
                // ดึงพนักงานทุกคนจาก ITPO_GetEmployeelist
                var employees = await GetAllEmployeesAsync();

                // ดึง role ที่กำหนดไว้
                using var conn = _db.GetBTITReqConnection();
                var roles = (await conn.QueryAsync<UserRoleModel>(
                    "ITPO_sp_GetAllUserRoles",
                    commandType: System.Data.CommandType.StoredProcedure)).ToList();

                vm.CurrentRoles = roles;

                // Join roles กับ employees (1 คนมีได้หลาย Role)
                var roleDict = roles
                    .GroupBy(r => r.SamAcc, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key.ToLower(),
                        g => g.Select(r => r.RoleName).ToList(),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var emp in employees)
                    emp.AssignedRoles = roleDict.TryGetValue(emp.SAMACC.ToLower(), out var rList)
                        ? rList : new List<string>();

                // Filter
                if (!string.IsNullOrWhiteSpace(search))
                    employees = employees.Where(e =>
                        e.DISPNAME.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.DISPNAME_TH.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.SAMACC.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.DEPART.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                if (!string.IsNullOrWhiteSpace(dept))
                    employees = employees.Where(e =>
                        e.DEPART.Equals(dept, StringComparison.OrdinalIgnoreCase)).ToList();

                vm.Employees = employees;
                vm.Departments = employees.Select(e => e.DEPART)
                    .Distinct().OrderBy(d => d).ToList();
                vm.TotalEmployees = vm.Employees.Count;
                vm.IssuerCount   = roles.Count(r => r.RoleName == "Issuer");
                vm.ApproverCount = roles.Count(r => r.RoleName == "Approver");
                vm.AdminCount    = roles.Count(r => r.RoleName == "Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AdminController.Index error");
                TempData["Error"] = $"Error loading employee list: {ex.Message}";
            }

            return View(vm);
        }

        // ── Add Role (AJAX) ───────────────────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetRole([FromBody] UpsertRoleRequest req)
        {
            if (!IsAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(req.RoleName) || req.RoleName == "User")
                return BadRequest(new { success = false, message = "Invalid role name" });

            var currentUser = SessionHelper.GetUser(HttpContext.Session)
                           ?? SessionHelper.GetUserFromClaims(User);
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_UpsertUserRole",
                    new
                    {
                        req.SamAcc, req.FullName, req.Email, req.Department,
                        req.RoleName,
                        CreatedBy = currentUser?.SamAcc
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
                _logger.LogInformation("Role {role} added to {sam} by {admin}",
                    req.RoleName, req.SamAcc, currentUser?.SamAcc);
                return Ok(new { success = true, message = $"{req.RoleName} added to {req.FullName}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetRole error for {sam}", req.SamAcc);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ── Remove Specific Role (AJAX) ───────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest req)
        {
            if (!IsAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(req.RoleName))
                return BadRequest(new { success = false, message = "RoleName required" });

            var currentUser = SessionHelper.GetUser(HttpContext.Session)
                           ?? SessionHelper.GetUserFromClaims(User);
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_DeleteUserRole",
                    new { SamAcc = req.SamAcc, RoleName = req.RoleName },
                    commandType: System.Data.CommandType.StoredProcedure);
                _logger.LogInformation("Role {role} removed from {sam} by {admin}",
                    req.RoleName, req.SamAcc, currentUser?.SamAcc);
                return Ok(new { success = true, message = $"{req.RoleName} removed from {req.FullName}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RemoveRole error for {sam}", req.SamAcc);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ── Current Role Assignments List ─────────────────────
        [HttpGet]
        public async Task<IActionResult> Roles()
        {
            if (!IsAdmin()) return Forbid();
            using var conn = _db.GetBTITReqConnection();
            var roles = (await conn.QueryAsync<UserRoleModel>(
                "ITPO_sp_GetAllUserRoles",
                commandType: System.Data.CommandType.StoredProcedure)).ToList();
            return View(roles);
        }

        // ── Email Logs (Admin only) ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> EmailLogs(
            DateTime? dateFrom, DateTime? dateTo,
            string? poNumber, string? mailType, bool? isSuccess, int page = 1)
        {
            if (!IsAdmin()) return Forbid();
            var (total, logs) = await _poService.GetEmailLogsAsync(
                dateFrom, dateTo, poNumber, mailType, isSuccess, page, 50);
            return View(new EmailLogViewModel
            {
                Logs       = logs,
                TotalCount = total,
                PageNum    = page,
                PageSize   = 50,
                DateFrom   = dateFrom,
                DateTo     = dateTo,
                PONumber   = poNumber,
                MailType   = mailType,
                IsSuccess  = isSuccess
            });
        }

        // ── Private: Get employees from ITPO_GetEmployeelist ──
        private async Task<List<EmployeeModel>> GetAllEmployeesAsync()
        {
            // ITPO_GetEmployeelist อาจอยู่ใน BT_HR หรือ BTITReq
            // ลอง BTITReq ก่อน ถ้าไม่มีให้ switch ไป BT_HR
            try
            {
                using var conn = _db.GetBTITReqConnection();
                var result = await conn.QueryAsync<EmployeeModel>(
                    "ITPO_GetEmployeelist",
                    commandType: System.Data.CommandType.StoredProcedure);
                return result.OrderBy(e => e.DISPNAME).ToList();
            }
            catch
            {
                try
                {
                    using var conn = _db.GetBT_HRConnection();
                    var result = await conn.QueryAsync<EmployeeModel>(
                        "ITPO_GetEmployeelist",
                        commandType: System.Data.CommandType.StoredProcedure);
                    return result.OrderBy(e => e.DISPNAME).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetAllEmployeesAsync: ITPO_GetEmployeelist not found in either DB");
                    return new List<EmployeeModel>();
                }
            }
        }
    }
}
