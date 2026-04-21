using BTITPORequest.Helpers;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BTITPORequest.Controllers
{
    [Authorize]
    public class PORequestController : Controller
    {
        private readonly IPOService _poService;
        private readonly IDigitalSignService _signService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<PORequestController> _logger;

        public PORequestController(
            IPOService poService,
            IDigitalSignService signService,
            IPdfService pdfService,
            ILogger<PORequestController> logger)
        {
            _poService = poService;
            _signService = signService;
            _pdfService = pdfService;
            _logger = logger;
        }

        // ── Helpers ───────────────────────────────────────────
        private UserSessionModel CurrentUser =>
            SessionHelper.GetUser(HttpContext.Session)
            ?? SessionHelper.GetUserFromClaims(User)
            ?? new UserSessionModel();

        // ── LIST ──────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(
            string? status, string? search, DateTime? dateFrom, DateTime? dateTo)
        {
            var user = CurrentUser;
            bool isAdmin = user.Role is "Admin";
            var list = await _poService.GetPOListAsync(user.SamAcc, isAdmin, dateFrom, dateTo, status);

            if (!string.IsNullOrWhiteSpace(search))
                list = list.Where(p =>
                    p.PONumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.VendorCompany.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Subject.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            return View(new POListViewModel
            {
                POList = list, FilterStatus = status, SearchText = search,
                DateFrom = dateFrom, DateTo = dateTo,
                TotalCount = list.Count, CurrentUserSam = user.SamAcc, IsAdmin = isAdmin
            });
        }

        // ── CREATE ────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = CurrentUser;
            var issuers   = await _poService.GetUsersByRoleAsync("Issuer");
            var approvers = await _poService.GetUsersByRoleAsync("Approver");

            return View(new POCreateViewModel
            {
                PO = new PORequestModel { PODate = DateTime.Today },
                CurrentUserSam  = user.SamAcc,
                CurrentUserName = user.FullName,
                Issuers   = issuers,
                Approvers = approvers
            });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromForm] PORequestModel po,
            [FromForm] string lineItemsJson,
            [FromForm] string selectedIssuerSam    = "",
            [FromForm] string selectedApprover1Sam = "",
            [FromForm] string selectedApprover2Sam = "",
            [FromForm] bool submitNow = false)
        {
            var user = CurrentUser;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();
            if (lineItems.Count == 0)
            {
                ModelState.AddModelError("", "Please add at least one line item.");
                var issuers   = await _poService.GetUsersByRoleAsync("Issuer");
                var approvers = await _poService.GetUsersByRoleAsync("Approver");
                return View(new POCreateViewModel
                {
                    PO = po, CurrentUserSam = user.SamAcc, CurrentUserName = user.FullName,
                    Issuers = issuers, Approvers = approvers,
                    SelectedIssuerSam = selectedIssuerSam,
                    SelectedApprover1Sam = selectedApprover1Sam,
                    SelectedApprover2Sam = selectedApprover2Sam
                });
            }
            RecalcTotals(po, lineItems);
            var poId = await _poService.CreatePOAsync(po, lineItems, user.SamAcc,
                selectedIssuerSam, selectedApprover1Sam, selectedApprover2Sam);

            if (submitNow)
                await DoSubmitAsync(poId, po.PONumber, user);

            TempData["Success"] = submitNow ? "PO submitted & signed successfully." : "PO saved as draft.";
            return RedirectToAction("Detail", new { id = poId });
        }

        // ── EDIT ──────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            if (po.RequesterSam != user.SamAcc && user.Role != "Admin") return Forbid();
            if (po.Status != POStatus.Draft) return BadRequest("Only drafts can be edited.");
            return View(new POCreateViewModel { PO = po, LineItems = po.LineItems, CurrentUserSam = user.SamAcc, CurrentUserName = user.FullName });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [FromForm] PORequestModel po,
            [FromForm] string lineItemsJson,
            [FromForm] bool submitNow = false)
        {
            var user = CurrentUser;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();
            RecalcTotals(po, lineItems);
            await _poService.UpdatePOAsync(po, lineItems);

            if (submitNow)
                await DoSubmitAsync(po.POId, po.PONumber, user);

            TempData["Success"] = submitNow ? "PO submitted & signed." : "PO updated.";
            return RedirectToAction("Detail", new { id = po.POId });
        }

        // ── DETAIL ────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            po.CanEdit   = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;
            po.CanSubmit = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;

            // CanIssue: Issuer role + ถ้ามี PreAssigned ต้องเป็น person นั้น
            po.CanIssue = po.Status == POStatus.Requested
                && (user.Role == "Admin"
                    || (user.Role == "Issuer"
                        && (string.IsNullOrEmpty(po.PreAssignedIssuerSam)
                            || po.PreAssignedIssuerSam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // CanApprove1: Approver role + ถ้ามี PreAssigned ต้องเป็น person นั้น
            po.CanApprove1 = po.Status == POStatus.Issued
                && (user.Role == "Admin"
                    || (user.Role == "Approver"
                        && (string.IsNullOrEmpty(po.PreAssignedApprover1Sam)
                            || po.PreAssignedApprover1Sam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // CanApprove2: ใช้ PreAssigned2 ถ้ามี ไม่งั้น fallback ไป PreAssigned1
            var preApp2 = !string.IsNullOrEmpty(po.PreAssignedApprover2Sam)
                          ? po.PreAssignedApprover2Sam : po.PreAssignedApprover1Sam;
            po.CanApprove2 = po.Status == POStatus.Authorized
                && (user.Role == "Admin"
                    || (user.Role == "Approver"
                        && (string.IsNullOrEmpty(preApp2)
                            || preApp2.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            po.CanDownloadPDF = po.Status == POStatus.Completed &&
                (po.RequesterSam == user.SamAcc || user.Role == "Admin");

            return View(po);
        }

        // ── SUBMIT ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Submit(int id)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            await DoSubmitAsync(id, po.PONumber, user);
            TempData["Success"] = "PO submitted & digitally signed.";
            return RedirectToAction("Detail", new { id });
        }

        // ── ISSUE ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Issue(int id)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            var signResult = await _signService.SignDataAsync(
                po.PONumber, "Issued", user.SamAcc, user.FullName, user.Department);

            await _poService.IssuePOAsync(id,
                user.SamAcc, user.FullName, user.Department,
                signResult?.SignatureBase64 ?? "",
                user.SignatureImageBase64);

            TempData["Success"] = "PO issued & digitally signed.";
            return RedirectToAction("Detail", new { id });
        }

        // ── APPROVE ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Approve(int id, int level)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            return View(new POApproveViewModel { PO = po, ApprovalLevel = level });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int level, string action, string? remark)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            if (action == "approve")
            {
                var purpose = level == 2 ? "Completed" : "Authorized";
                var signResult = await _signService.SignDataAsync(
                    po.PONumber, purpose, user.SamAcc, user.FullName, user.Department, remark);

                await _poService.ApprovePOAsync(id, level,
                    user.SamAcc, user.FullName, user.Department,
                    signResult?.SignatureBase64 ?? "",
                    user.SignatureImageBase64, remark);

                TempData["Success"] = level == 2 ? "PO fully approved! Document complete." : "PO approved — moving to final step.";
            }
            else
            {
                await _poService.RejectPOAsync(id, level, user.SamAcc, user.FullName, remark ?? "");
                TempData["Warning"] = "PO rejected and returned for revision.";
            }
            return RedirectToAction("Detail", new { id });
        }

        // ── DOWNLOAD PDF ──────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            if (po.RequesterSam != user.SamAcc && user.Role != "Admin") return Forbid();
            if (po.Status != POStatus.Completed) return BadRequest("PDF is only available for completed POs.");

            var pdfBytes = await _pdfService.GeneratePOPdfAsync(po, user.SamAcc, user.FullName);
            return File(pdfBytes, "application/pdf", $"PO_{po.PONumber}_{po.PODate:yyyyMMdd}.pdf");
        }

        // ── PRIVATE: Do Submit ────────────────────────────────
        private async Task DoSubmitAsync(int poId, string poNumber, UserSessionModel user)
        {
            var signResult = await _signService.SignDataAsync(
                poNumber, "Requested", user.SamAcc, user.FullName, user.Department);

            await _poService.SubmitPOAsync(poId, user.SamAcc,
                signResult?.SignatureBase64 ?? "",
                user.SignatureImageBase64);
        }

        // ── PRIVATE: Recalc Totals ────────────────────────────
        private static void RecalcTotals(PORequestModel po, List<POLineItemModel> items)
        {
            po.Total = items.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
            po.VatAmount = Math.Round(po.Total * po.VatPercent / 100, 2);
            po.GrandTotal = po.Total + po.VatAmount;
            po.GrandTotalText = NumberToWords(po.GrandTotal);
        }

        private static string NumberToWords(decimal amount)
        {
            var baht = (long)Math.Floor(amount);
            var satang = (int)Math.Round((amount - baht) * 100);
            var words = ToWords(baht) + " Baht";
            if (satang > 0) words += $" and {ToWords(satang)} Satang";
            return words + " Only";
        }

        private static string ToWords(long n)
        {
            if (n == 0) return "Zero";
            string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
                "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
                "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            if (n < 20) return ones[n];
            if (n < 100) return tens[n / 10] + (n % 10 > 0 ? " " + ones[n % 10] : "");
            if (n < 1000) return ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + ToWords(n % 100) : "");
            if (n < 1_000_000) return ToWords(n / 1000) + " Thousand" + (n % 1000 > 0 ? " " + ToWords(n % 1000) : "");
            if (n < 1_000_000_000) return ToWords(n / 1_000_000) + " Million" + (n % 1_000_000 > 0 ? " " + ToWords(n % 1_000_000) : "");
            return ToWords(n / 1_000_000_000) + " Billion" + (n % 1_000_000_000 > 0 ? " " + ToWords(n % 1_000_000_000) : "");
        }
    }
}
