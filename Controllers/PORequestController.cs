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

        // ─── LIST ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(
            string? status, string? search, DateTime? dateFrom, DateTime? dateTo)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            bool isAdmin = user.Role is "Admin";

            var list = await _poService.GetPOListAsync(user.SamAcc, isAdmin, dateFrom, dateTo, status);

            if (!string.IsNullOrWhiteSpace(search))
                list = list.Where(p =>
                    p.PONumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.VendorCompany.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Subject.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            var vm = new POListViewModel
            {
                POList = list,
                FilterStatus = status,
                SearchText = search,
                DateFrom = dateFrom,
                DateTo = dateTo,
                TotalCount = list.Count,
                CurrentUserSam = user.SamAcc,
                IsAdmin = isAdmin
            };
            return View(vm);
        }

        // ─── CREATE ───────────────────────────────────────────────
        [HttpGet]
        public IActionResult Create()
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var vm = new POCreateViewModel
            {
                PO = new PORequestModel { PODate = DateTime.Today },
                CurrentUserSam = user.SamAcc,
                CurrentUserName = user.FullName
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromForm] PORequestModel po,
            [FromForm] string lineItemsJson,
            [FromForm] bool submitNow = false)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();

            if (lineItems.Count == 0)
            {
                ModelState.AddModelError("", "Please add at least one line item.");
                return View(new POCreateViewModel { PO = po, CurrentUserSam = user.SamAcc, CurrentUserName = user.FullName });
            }

            RecalcTotals(po, lineItems);

            var poId = await _poService.CreatePOAsync(po, lineItems, user.SamAcc);

            if (submitNow)
            {
                // Call digital sign API to get requester signature
                var (signOk, signUrl, signErr) = await _signService.RequestSignatureAsync(user.Token, poId, "ITPO");
                var url = signOk ? (signUrl ?? "") : "";
                await _poService.SubmitPOAsync(poId, user.SamAcc, url);
                TempData["Success"] = "PO submitted successfully. Awaiting issuance.";
            }
            else
            {
                TempData["Success"] = "PO saved as draft.";
            }

            return RedirectToAction("Detail", new { id = poId });
        }

        // ─── EDIT ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            if (po.RequesterSam != user.SamAcc && user.Role != "Admin") return Forbid();
            if (po.Status != POStatus.Draft) return BadRequest("Only draft POs can be edited.");

            return View(new POCreateViewModel
            {
                PO = po,
                LineItems = po.LineItems,
                CurrentUserSam = user.SamAcc,
                CurrentUserName = user.FullName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [FromForm] PORequestModel po,
            [FromForm] string lineItemsJson,
            [FromForm] bool submitNow = false)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();

            RecalcTotals(po, lineItems);
            await _poService.UpdatePOAsync(po, lineItems);

            if (submitNow)
            {
                var (signOk, signUrl, _) = await _signService.RequestSignatureAsync(user.Token, po.POId, "ITPO");
                await _poService.SubmitPOAsync(po.POId, user.SamAcc, signOk ? (signUrl ?? "") : "");
                TempData["Success"] = "PO submitted for approval.";
            }
            else
            {
                TempData["Success"] = "PO updated.";
            }

            return RedirectToAction("Detail", new { id = po.POId });
        }

        // ─── DETAIL ───────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            // Set permission flags
            po.CanEdit = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;
            po.CanSubmit = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;

            // Issuance: any user with Issuer role or Admin
            po.CanIssue = po.Status == POStatus.Requested
                && (user.Role is "Issuer" or "Admin");

            // First approval
            po.CanApprove1 = po.Status == POStatus.Issued
                && (user.Role is "Approver" or "Admin");

            // Second approval
            po.CanApprove2 = po.Status == POStatus.Authorized
                && (user.Role is "Approver" or "Admin");

            // Download PDF when completed
            po.CanDownloadPDF = po.Status == POStatus.Completed
                && (po.RequesterSam == user.SamAcc || user.Role == "Admin");

            return View(po);
        }

        // ─── SUBMIT (Requested) ───────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Submit(int id)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var (signOk, signUrl, err) = await _signService.RequestSignatureAsync(user.Token, id, "ITPO");
            if (!signOk) _logger.LogWarning("Digital sign failed for PO {id}: {err}", id, err);

            await _poService.SubmitPOAsync(id, user.SamAcc, signOk ? (signUrl ?? "") : "");
            TempData["Success"] = "PO submitted. Waiting for issuance.";
            return RedirectToAction("Detail", new { id });
        }

        // ─── ISSUE ────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Issue(int id)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var (signOk, signUrl, err) = await _signService.ApproveSignatureAsync(user.Token, id, "ITPO");

            await _poService.IssuePOAsync(id, user.SamAcc, user.FullName,
                User.FindFirst("title")?.Value ?? "Network Administrator",
                signOk ? (signUrl ?? "") : "");

            TempData["Success"] = "PO issued successfully.";
            return RedirectToAction("Detail", new { id });
        }

        // ─── APPROVE ──────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Approve(int id, int level)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            return View(new POApproveViewModel { PO = po, ApprovalLevel = level });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int level, string action, string? remark)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;

            if (action == "approve")
            {
                var (signOk, signUrl, _) = await _signService.ApproveSignatureAsync(user.Token, id, "ITPO");
                await _poService.ApprovePOAsync(id, level, user.SamAcc, user.FullName,
                    User.FindFirst("title")?.Value ?? "Section Head",
                    signOk ? (signUrl ?? "") : "", remark);
                TempData["Success"] = level == 2 ? "PO fully approved! Document is complete." : "PO approved. Moving to next level.";
            }
            else
            {
                await _poService.RejectPOAsync(id, level, user.SamAcc, user.FullName, remark ?? "");
                TempData["Warning"] = "PO rejected and returned for revision.";
            }

            return RedirectToAction("Detail", new { id });
        }

        // ─── DOWNLOAD PDF ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var user = SessionHelper.GetUserFromClaims(User)!;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            // Only requester or admin can download
            if (po.RequesterSam != user.SamAcc && user.Role != "Admin")
                return Forbid();

            if (po.Status != POStatus.Completed)
                return BadRequest("PDF is only available for completed POs.");

            var pdfBytes = await _pdfService.GeneratePOPdfAsync(po);
            var fileName = $"PO_{po.PONumber}_{po.PODate:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // ─── SIGN CALLBACK (from DigitalSign API) ─────────────────
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SignCallback([FromBody] SignCallbackPayload payload)
        {
            _logger.LogInformation("Sign callback received: PO={poId} Status={status}", payload.DocumentId, payload.Status);
            // Handle callback from digital sign API if needed
            await Task.CompletedTask;
            return Ok(new { success = true });
        }

        // ─── HELPERS ──────────────────────────────────────────────
        private static void RecalcTotals(PORequestModel po, List<POLineItemModel> items)
        {
            po.Total = items.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
            po.VatAmount = Math.Round(po.Total * po.VatPercent / 100, 2);
            po.GrandTotal = po.Total + po.VatAmount;
            po.GrandTotalText = NumberToThaiWords(po.GrandTotal);
        }

        private static string NumberToThaiWords(decimal amount)
        {
            // Simplified English amount-in-words
            var baht = (long)Math.Floor(amount);
            var satang = (int)Math.Round((amount - baht) * 100);
            var words = ConvertToWords(baht) + " Baht";
            if (satang > 0) words += $" and {ConvertToWords(satang)} Satang";
            return words + " Only";
        }

        private static string ConvertToWords(long number)
        {
            if (number == 0) return "Zero";
            string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
                              "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
                              "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20) return ones[number];
            if (number < 100) return tens[number / 10] + (number % 10 > 0 ? " " + ones[number % 10] : "");
            if (number < 1000) return ones[number / 100] + " Hundred" + (number % 100 > 0 ? " " + ConvertToWords(number % 100) : "");
            if (number < 1_000_000) return ConvertToWords(number / 1000) + " Thousand" + (number % 1000 > 0 ? " " + ConvertToWords(number % 1000) : "");
            if (number < 1_000_000_000) return ConvertToWords(number / 1_000_000) + " Million" + (number % 1_000_000 > 0 ? " " + ConvertToWords(number % 1_000_000) : "");
            return ConvertToWords(number / 1_000_000_000) + " Billion" + (number % 1_000_000_000 > 0 ? " " + ConvertToWords(number % 1_000_000_000) : "");
        }
    }

    public class SignCallbackPayload
    {
        public string DocumentId { get; set; } = "";
        public string Status { get; set; } = "";
        public string? SignatureUrl { get; set; }
    }
}
