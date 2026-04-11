using BTITPORequest.Helpers;
using BTITPORequest.Models;
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

        public AdminController(DbContext db, IConfiguration config, ILogger<AdminController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
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

                // Join roles กับ employees
                var roleDict = roles.ToDictionary(
                    r => r.SamAcc.ToLower(),
                    r => r.RoleName,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var emp in employees)
                    emp.AssignedRole = roleDict.TryGetValue(emp.SAMACC.ToLower(), out var r) ? r : null;

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

        // ── Assign / Update Role (AJAX) ───────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetRole([FromBody] UpsertRoleRequest req)
        {
            if (!IsAdmin()) return Forbid();

            var currentUser = SessionHelper.GetUser(HttpContext.Session)
                           ?? SessionHelper.GetUserFromClaims(User);

            try
            {
                using var conn = _db.GetBTITReqConnection();

                if (string.IsNullOrEmpty(req.RoleName) || req.RoleName == "User")
                {
                    // Remove role
                    await conn.ExecuteAsync("ITPO_sp_DeleteUserRole",
                        new { SamAcc = req.SamAcc },
                        commandType: System.Data.CommandType.StoredProcedure);
                    _logger.LogInformation("Role removed for {sam} by {admin}", req.SamAcc, currentUser?.SamAcc);
                    return Ok(new { success = true, message = $"Role removed for {req.FullName}" });
                }
                else
                {
                    // Assign or update role
                    await conn.ExecuteAsync("ITPO_sp_UpsertUserRole",
                        new
                        {
                            req.SamAcc, req.FullName, req.Email, req.Department,
                            req.RoleName,
                            CreatedBy = currentUser?.SamAcc
                        },
                        commandType: System.Data.CommandType.StoredProcedure);
                    _logger.LogInformation("Role {role} assigned to {sam} by {admin}",
                        req.RoleName, req.SamAcc, currentUser?.SamAcc);
                    return Ok(new { success = true, message = $"{req.RoleName} assigned to {req.FullName}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetRole error for {sam}", req.SamAcc);
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
