using BTITPORequest.Data;
using BTITPORequest.Helpers;
using BTITPORequest.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers
{
    [Authorize]
    public class VendorController : Controller
    {
        private readonly DbContext _db;
        private readonly ILogger<VendorController> _logger;

        public VendorController(DbContext db, ILogger<VendorController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private bool IsAdmin()
        {
            var user = SessionHelper.GetUser(HttpContext.Session)
                    ?? SessionHelper.GetUserFromClaims(User);
            return user?.Role == "Admin";
        }

        // ── INDEX ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            if (!IsAdmin()) return Forbid();
            using var conn = _db.GetBTITReqConnection();
            var vendors = (await conn.QueryAsync<VendorInfoModel>(
                "ITPO_sp_GetVendors",
                new { Search = string.IsNullOrWhiteSpace(search) ? null : search },
                commandType: System.Data.CommandType.StoredProcedure)).ToList();
            return View(new VendorIndexViewModel { Vendors = vendors, SearchText = search });
        }

        // ── API: Search (JSON) สำหรับ Create PO autocomplete ──
        [HttpGet]
        public async Task<IActionResult> Search(string? q)
        {
            using var conn = _db.GetBTITReqConnection();
            var vendors = await conn.QueryAsync<VendorInfoModel>(
                "ITPO_sp_GetVendors",
                new { Search = string.IsNullOrWhiteSpace(q) ? null : q },
                commandType: System.Data.CommandType.StoredProcedure);
            return Json(vendors.Take(10));
        }

        // ── CREATE ────────────────────────────────────────────
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAdmin()) return Forbid();
            return View(new VendorInfoModel());
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VendorInfoModel model)
        {
            if (!IsAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(model);
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteScalarAsync<int>("ITPO_sp_CreateVendor",
                    new { model.VendorAttn, model.VendorCompany, model.VendorAddress,
                          model.VendorTel, model.VendorFax, model.VendorEmail },
                    commandType: System.Data.CommandType.StoredProcedure);
                TempData["Success"] = $"เพิ่ม {model.VendorCompany} เรียบร้อยแล้ว";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateVendor error");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // ── EDIT ──────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin()) return Forbid();
            using var conn = _db.GetBTITReqConnection();
            var v = await conn.QueryFirstOrDefaultAsync<VendorInfoModel>(
                "ITPO_sp_GetVendorById", new { VendorId = id },
                commandType: System.Data.CommandType.StoredProcedure);
            if (v == null) return NotFound();
            return View(v);
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(VendorInfoModel model)
        {
            if (!IsAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(model);
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_UpdateVendor",
                    new { model.VendorId, model.VendorAttn, model.VendorCompany,
                          model.VendorAddress, model.VendorTel, model.VendorFax, model.VendorEmail },
                    commandType: System.Data.CommandType.StoredProcedure);
                TempData["Success"] = $"แก้ไข {model.VendorCompany} เรียบร้อยแล้ว";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateVendor error");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // ── DELETE (AJAX) ─────────────────────────────────────
        [HttpPost][IgnoreAntiforgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin()) return Forbid();
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_DeleteVendor",
                    new { VendorId = id },
                    commandType: System.Data.CommandType.StoredProcedure);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteVendor error");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
