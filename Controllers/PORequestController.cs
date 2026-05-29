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
        private readonly SendMailController _mail;
        private readonly ILogger<PORequestController> _logger;

        public PORequestController(
            IPOService poService,
            IDigitalSignService signService,
            IPdfService pdfService,
            SendMailController mail,
            ILogger<PORequestController> logger)
        {
            _poService = poService;
            _signService = signService;
            _pdfService = pdfService;
            _mail = mail;
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
                POList = list,
                FilterStatus = status,
                SearchText = search,
                DateFrom = dateFrom,
                DateTo = dateTo,
                TotalCount = list.Count,
                CurrentUserSam = user.SamAcc,
                IsAdmin = isAdmin
            });
        }

        // ── CREATE ────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = CurrentUser;
            var issuers = await _poService.GetUsersByRoleAsync("Issuer");
            var approvers = await _poService.GetUsersByRoleAsync("Approver");
            return View(new POCreateViewModel
            {
                PO = new PORequestModel { PODate = DateTime.Today },
                CurrentUserSam = user.SamAcc,
                CurrentUserName = user.FullName,
                Issuers = issuers,
                Approvers = approvers
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromForm] PORequestModel po,
            [FromForm] string lineItemsJson,
            [FromForm] string selectedIssuerSam = "",
            [FromForm] string selectedApprover1Sam = "",
            [FromForm] string selectedApprover2Sam = "",
            [FromForm] bool submitNow = false)
        {
            var user = CurrentUser;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();

            // Line items validation เท่านั้น — Issuer/Approver ตรวจโดย JS แล้ว
            if (lineItems.Count == 0)
            {
                var issuers   = await _poService.GetUsersByRoleAsync("Issuer");
                var approvers = await _poService.GetUsersByRoleAsync("Approver");
                ModelState.AddModelError("", "กรุณาเพิ่มรายการสินค้า (Line Items) อย่างน้อย 1 รายการ");
                return View(new POCreateViewModel
                {
                    PO = po,
                    CurrentUserSam = user.SamAcc,
                    CurrentUserName = user.FullName,
                    Issuers = issuers,
                    Approvers = approvers,
                    SelectedIssuerSam    = selectedIssuerSam,
                    SelectedApprover1Sam = selectedApprover1Sam,
                    SelectedApprover2Sam = selectedApprover2Sam
                });
            }

            RecalcTotals(po, lineItems);
            var poId = await _poService.CreatePOAsync(po, lineItems, user.SamAcc,
                selectedIssuerSam, selectedApprover1Sam, selectedApprover2Sam);

            if (submitNow) await DoSubmitAsync(poId, po.PONumber, user);

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
            var user = CurrentUser;
            var lineItems = JsonConvert.DeserializeObject<List<POLineItemModel>>(lineItemsJson ?? "[]") ?? new();
            RecalcTotals(po, lineItems);
            await _poService.UpdatePOAsync(po, lineItems);
            if (submitNow) await DoSubmitAsync(po.POId, po.PONumber, user);
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

            po.CanEdit = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;
            po.CanSubmit = po.Status == POStatus.Draft && po.RequesterSam == user.SamAcc;

            po.CanIssue = po.Status == POStatus.Requested
                && (user.Role == "Admin"
                    || (user.Role == "Issuer"
                        && (string.IsNullOrEmpty(po.PreAssignedIssuerSam)
                            || po.PreAssignedIssuerSam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // CanRejectIssue: Issuer reject กลับ Requester (Status=1)
            // Admin reject ได้ทุก pending status
            po.CanRejectIssue = po.Status == POStatus.Requested
                && (user.Role == "Admin"
                    || (user.Role == "Issuer"
                        && (string.IsNullOrEmpty(po.PreAssignedIssuerSam)
                            || po.PreAssignedIssuerSam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // CanRejectApprove: Admin หรือ Approver reject ตอน Issued (Status=2)
            po.CanRejectApprove = po.Status == POStatus.Issued
                && (user.Role == "Admin"
                    || (user.Role == "Approver"
                        && (string.IsNullOrEmpty(po.PreAssignedApprover1Sam)
                            || po.PreAssignedApprover1Sam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // CanApprove: Approver role + PreAssigned check → Issued → Completed โดยตรง
            po.CanApprove1 = po.Status == POStatus.Issued
                && (user.Role == "Admin"
                    || (user.Role == "Approver"
                        && (string.IsNullOrEmpty(po.PreAssignedApprover1Sam)
                            || po.PreAssignedApprover1Sam.Equals(user.SamAcc, StringComparison.OrdinalIgnoreCase))));

            // ไม่มี CanApprove2 อีกต่อไป — 1 level approval เท่านั้น
            po.CanApprove2 = false;

            // ทุกคนที่ login แล้วสามารถ Download PDF ได้เมื่อ Completed
            po.CanDownloadPDF = po.Status == POStatus.Completed;

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

        // ── REJECT APPROVE (Approver/Admin ส่งกลับ Requester) ─
        [HttpGet]
        public async Task<IActionResult> RejectApprove(int id)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            return View("RejectIssue", new POApproveViewModel { PO = po, ApprovalLevel = 1 });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApprove(int id, string? remark)
        {
            var user = CurrentUser;
            var po   = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            await _poService.RejectPOAsync(id, 1, user.SamAcc, user.FullName, remark ?? "");

            var reqEmail = await GetEmailBysamAccAsync(po.RequesterSam);
            if (!string.IsNullOrEmpty(reqEmail))
                _ = _mail.NotifyRejectedAsync(reqEmail, po.PONumber, po.VendorCompany,
                        po.Subject, user.FullName, remark ?? "", id, 1);

            TempData["Warning"] = "PO rejected — returned to Requester for revision.";
            return RedirectToAction("Detail", new { id });
        }

        // ── REJECT ISSUE (Issuer ส่งกลับ Requester) ──────────
        [HttpGet]
        public async Task<IActionResult> RejectIssue(int id)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            return View(new POApproveViewModel { PO = po, ApprovalLevel = 0 });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectIssue(int id, string? remark)
        {
            var user = CurrentUser;
            var po   = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            // Reject กลับ Status = -1 (level 1 = Issuer reject)
            await _poService.RejectPOAsync(id, 1, user.SamAcc, user.FullName, remark ?? "");

            // แจ้ง Requester
            var reqEmail = await GetEmailBysamAccAsync(po.RequesterSam);
            if (!string.IsNullOrEmpty(reqEmail))
                _ = _mail.NotifyRejectedAsync(reqEmail, po.PONumber, po.VendorCompany,
                        po.Subject, user.FullName, remark ?? "", id, 1);

            TempData["Warning"] = "PO rejected — returned to Requester for revision.";
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
            var sigImage = await GetOrFetchSignatureAsync(user);

            await _poService.IssuePOAsync(id,
                user.SamAcc, user.FullName, user.Department,
                signResult?.SignatureBase64 ?? "", sigImage);

            // แจ้ง Approver
            if (!string.IsNullOrEmpty(po.PreAssignedApprover1Sam))
            {
                var approvers = await _poService.GetUsersByRoleAsync("Approver");
                var approver = approvers.FirstOrDefault(a =>
                    a.SamAcc.Equals(po.PreAssignedApprover1Sam, StringComparison.OrdinalIgnoreCase));
                if (approver != null && !string.IsNullOrEmpty(approver.Email))
                    _ = _mail.NotifyApproverAsync(approver.Email, po.PONumber, po.VendorCompany,
                            po.Subject, user.FullName, po.GrandTotal.ToString("N2"), id);
            }

            // แจ้ง Requester ว่า PO Issued แล้ว
            var reqEmail = await GetEmailBysamAccAsync(po.RequesterSam);
            if (!string.IsNullOrEmpty(reqEmail))
                _ = _mail.NotifyRequesterIssuedAsync(reqEmail, po.PONumber, po.VendorCompany,
                        po.Subject, user.FullName, po.GrandTotal.ToString("N2"), id);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int level, string action, string? remark)
        {
            var user = CurrentUser;
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();

            if (action == "approve")
            {
                // Single level approval: Issued → Completed โดยตรง
                var signResult = await _signService.SignDataAsync(
                    po.PONumber, "Completed", user.SamAcc, user.FullName, user.Department, remark);
                var sigImage = await GetOrFetchSignatureAsync(user);

                await _poService.ApprovePOAsync(id, 1,
                    user.SamAcc, user.FullName, user.Department,
                    signResult?.SignatureBase64 ?? "", sigImage, remark);

                // แจ้ง Requester ว่า PO Completed — ผลการอนุมัติ
                var reqEmail = await GetEmailBysamAccAsync(po.RequesterSam);
                if (!string.IsNullOrEmpty(reqEmail))
                    _ = _mail.NotifyCompletedAsync(reqEmail, po.PONumber, po.VendorCompany,
                            po.Subject, user.FullName, po.GrandTotal.ToString("N2"), id);

                TempData["Success"] = "PO approved & completed! Ready to download PDF.";
            }
            else
            {
                await _poService.RejectPOAsync(id, level, user.SamAcc, user.FullName, remark ?? "");

                // แจ้ง Requester ว่าถูก Reject
                var reqEmail = await GetEmailBysamAccAsync(po.RequesterSam);
                if (!string.IsNullOrEmpty(reqEmail))
                    _ = _mail.NotifyRejectedAsync(reqEmail, po.PONumber, po.VendorCompany,
                            po.Subject, user.FullName, remark ?? "", id, level);

                TempData["Warning"] = "PO rejected and returned for revision.";
            }
            return RedirectToAction("Detail", new { id });
        }

        // ── MANUAL EMAIL SEND (Admin only) ───────────────────
        /// <summary>
        /// Admin กดส่ง email ซ้ำได้ทันที ไม่กระทบ flow ปกติ
        /// POST /PORequest/ManualSendEmail?id={poId}&type={issuer|approver|requester|completed}
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ManualSendEmail(int id, string type)
        {
            var user = CurrentUser;
            if (user.Role != "Admin")
                return Json(new { success = false, message = "Admin only" });

            var po = await _poService.GetPOByIdAsync(id);
            if (po == null)
                return Json(new { success = false, message = "PO not found" });

            bool sent = false;
            string target = "";

            switch (type.ToLower())
            {
                case "issuer":
                    // ส่งแจ้ง Issuer ให้ Issue PO
                    var issuerSam = !string.IsNullOrEmpty(po.IssuerSam)
                                    ? po.IssuerSam : po.PreAssignedIssuerSam;
                    if (!string.IsNullOrEmpty(issuerSam))
                    {
                        var issuers = await _poService.GetUsersByRoleAsync("Issuer");
                        var issuer = issuers.FirstOrDefault(i =>
                            i.SamAcc.Equals(issuerSam, StringComparison.OrdinalIgnoreCase));
                        var email = issuer?.Email ?? await GetEmailBysamAccAsync(issuerSam);
                        target = email;
                        sent = await _mail.NotifyIssuerAsync(email, po.PONumber, po.VendorCompany,
                            po.Subject, po.RequesterName ?? po.RequesterSam, "",
                            po.GrandTotal.ToString("N2"), id);
                    }
                    break;

                case "approver":
                    // ส่งแจ้ง Approver ให้ Authorize PO
                    var approverSam = !string.IsNullOrEmpty(po.Approver1Sam)
                                      ? po.Approver1Sam : po.PreAssignedApprover1Sam;
                    if (!string.IsNullOrEmpty(approverSam))
                    {
                        var approvers = await _poService.GetUsersByRoleAsync("Approver");
                        var approver = approvers.FirstOrDefault(a =>
                            a.SamAcc.Equals(approverSam, StringComparison.OrdinalIgnoreCase));
                        var email = approver?.Email ?? await GetEmailBysamAccAsync(approverSam);
                        target = email;
                        sent = await _mail.NotifyApproverAsync(email, po.PONumber, po.VendorCompany,
                            po.Subject, po.IssuerName ?? po.IssuerSam ?? "",
                            po.GrandTotal.ToString("N2"), id);
                    }
                    break;

                case "requester":
                    // ส่งแจ้ง Requester ว่า PO ถูก Issued แล้ว
                    var reqEmail1 = await GetEmailBysamAccAsync(po.RequesterSam);
                    target = reqEmail1;
                    sent = await _mail.NotifyRequesterIssuedAsync(reqEmail1, po.PONumber,
                        po.VendorCompany, po.Subject, po.IssuerName ?? "",
                        po.GrandTotal.ToString("N2"), id);
                    break;

                case "completed":
                    // ส่งแจ้ง Requester ว่า PO Completed
                    var reqEmail2 = await GetEmailBysamAccAsync(po.RequesterSam);
                    target = reqEmail2;
                    sent = await _mail.NotifyCompletedAsync(reqEmail2, po.PONumber,
                        po.VendorCompany, po.Subject,
                        po.Approver1Name ?? po.Approver1Sam ?? "",
                        po.GrandTotal.ToString("N2"), id);
                    break;

                default:
                    return Json(new { success = false, message = $"Unknown type: {type}" });
            }

            var msg = sent
                ? $"✅ ส่ง email ไปที่ {target} เรียบร้อย"
                : $"❌ ส่ง email ไม่สำเร็จ (ตรวจสอบ email log ที่ logs/email/)";

            return Json(new { success = sent, message = msg, to = target });
        }

        // ── PRINT PDF (Blank Form — content only, no header/footer) ──
        [HttpGet]
        public async Task<IActionResult> PrintPdf(int id)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            if (po.Status != POStatus.Completed)
                return BadRequest("PDF is only available for completed POs.");

            // ── Fetch signature images ที่ขาดหายไปจาก DB ────────
            // เหมือน lazy-fetch ใน Detail.cshtml แต่ทำ server-side ก่อน generate PDF
            po = await FillMissingSignatureImagesAsync(po);

            var pdfBytes = await _pdfService.GeneratePOPdfForPrintAsync(po);
            return File(pdfBytes, "application/pdf",
                $"PO_{po.PONumber}_{po.PODate:yyyyMMdd}_print.pdf");
        }

        // ── DOWNLOAD PDF (Digital — full with header/footer + digital sign) ──
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var po = await _poService.GetPOByIdAsync(id);
            if (po == null) return NotFound();
            if (po.Status != POStatus.Completed)
                return BadRequest("PDF is only available for completed POs.");

            // Fetch missing signatures ก่อน generate
            po = await FillMissingSignatureImagesAsync(po);

            var user = CurrentUser;
            var pdfBytes = await _pdfService.GeneratePOPdfAsync(po, user.SamAcc, user.FullName);
            return File(pdfBytes, "application/pdf",
                $"PO_{po.PONumber}_{po.PODate:yyyyMMdd}.pdf");
        }

        // ── PRIVATE: Fetch missing signature images from bt_digitalsign ──────
        /// <summary>
        /// สำหรับแต่ละช่อง (Requester/Issuer/Approver1/Approver2)
        /// ถ้า SignatureImage ว่างใน DB แต่มี SamAcc → fetch จาก API แทน
        /// เหมือน lazy-fetch ใน Detail.cshtml แต่ทำ server-side
        /// </summary>
        private async Task<PORequestModel> FillMissingSignatureImagesAsync(PORequestModel po)
        {
            // Always fetch จาก API (ไม่ใช้ cached ใน DB) เพื่อให้ได้ลายเซ็นล่าสุดเสมอ
            var tasks = new List<Task>();

            if (!string.IsNullOrEmpty(po.RequesterSam))
                tasks.Add(Task.Run(async () =>
                    po.RequesterSignatureImage = await FetchSigSafe(po.RequesterSam) ?? string.Empty));

            if (!string.IsNullOrEmpty(po.IssuerSam))
                tasks.Add(Task.Run(async () =>
                    po.IssuerSignatureImage = await FetchSigSafe(po.IssuerSam) ?? string.Empty));

            if (!string.IsNullOrEmpty(po.Approver1Sam))
                tasks.Add(Task.Run(async () =>
                    po.Approver1SignatureImage = await FetchSigSafe(po.Approver1Sam) ?? string.Empty));

            if (!string.IsNullOrEmpty(po.Approver2Sam))
                tasks.Add(Task.Run(async () =>
                    po.Approver2SignatureImage = await FetchSigSafe(po.Approver2Sam) ?? string.Empty));

            if (tasks.Count > 0)
            {
                try { await Task.WhenAll(tasks); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FillMissingSignatureImagesAsync partial failure for PO {id}", po.POId);
                }
            }

            return po;
        }

        private async Task<string?> FetchSigSafe(string samAcc)
        {
            try { return await _signService.GetSignatureImageAsync(samAcc); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FetchSigSafe failed for {sam}", samAcc);
                return null;
            }
        }

        // ── PRIVATE: Do Submit → notify Issuer ───────────────
        private async Task DoSubmitAsync(int poId, string poNumber, UserSessionModel user)
        {
            var signResult = await _signService.SignDataAsync(
                poNumber, "Requested", user.SamAcc, user.FullName, user.Department);
            var sigImage = await GetOrFetchSignatureAsync(user);
            var title = user.Department;

            await _poService.SubmitPOAsync(poId, user.SamAcc,
                user.FullName, title,
                signResult?.SignatureBase64 ?? "", sigImage);

            var po = await _poService.GetPOByIdAsync(poId);
            if (po == null) return;

            // ── แจ้ง Requester (ตัวเอง) ว่า PO ถูก Submit แล้ว ─
            var requesterEmail = await GetEmailBysamAccAsync(user.SamAcc);
            var issuerName = string.Empty;
            if (!string.IsNullOrEmpty(po.PreAssignedIssuerSam))
            {
                var issuers = await _poService.GetUsersByRoleAsync("Issuer");
                var issuer = issuers.FirstOrDefault(i =>
                    i.SamAcc.Equals(po.PreAssignedIssuerSam, StringComparison.OrdinalIgnoreCase));
                issuerName = issuer?.FullName ?? po.PreAssignedIssuerSam;

                // ── แจ้ง Issuer ให้ Issue ──────────────────────
                if (issuer != null && !string.IsNullOrEmpty(issuer.Email))
                    _ = _mail.NotifyIssuerAsync(issuer.Email, po.PONumber, po.VendorCompany,
                            po.Subject, user.FullName, user.Department,
                            po.GrandTotal.ToString("N2"), poId);
            }

            // ── แจ้ง Requester ว่า PO ถูก Submit และกำลังรอ Issued ─
            if (!string.IsNullOrEmpty(requesterEmail))
                _ = _mail.NotifyRequesterSubmittedAsync(requesterEmail, po.PONumber,
                        po.VendorCompany, po.Subject, issuerName,
                        po.GrandTotal.ToString("N2"), poId);
        }

        // ── PRIVATE: Signature image — session → API fallback ─
        private async Task<string> GetOrFetchSignatureAsync(UserSessionModel user)
        {
            // 1. มีใน Session แล้ว → ใช้เลย
            if (!string.IsNullOrEmpty(user.SignatureImageBase64))
                return user.SignatureImageBase64;

            // 2. ไม่มี → fetch ใหม่จาก bt_digitalsign API
            try
            {
                var img = await _signService.GetSignatureImageAsync(user.SamAcc);
                if (!string.IsNullOrEmpty(img))
                {
                    user.SignatureImageBase64 = img;
                    SessionHelper.SetUser(HttpContext.Session, user);
                }
                return img ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetOrFetchSignatureAsync failed for {sam}", user.SamAcc);
                return string.Empty;
            }
        }

        // ── PRIVATE: Get email by samAcc from ITPO_UserRoles ──
        // ใช้สำหรับส่ง email แจ้ง Requester
        // (ถ้ามีใน UserRoles → ใช้ email นั้น ไม่งั้น fallback เป็น samAcc@domain)
        private async Task<string> GetEmailBysamAccAsync(string samAcc)
        {
            if (string.IsNullOrEmpty(samAcc)) return string.Empty;
            try
            {
                // ลอง Issuer + Approver list ก่อน
                var allRoles = await _poService.GetUsersByRoleAsync("Issuer");
                allRoles.AddRange(await _poService.GetUsersByRoleAsync("Approver"));
                allRoles.AddRange(await _poService.GetUsersByRoleAsync("Admin"));

                var found = allRoles.FirstOrDefault(u =>
                    u.SamAcc.Equals(samAcc, StringComparison.OrdinalIgnoreCase));
                if (found != null && !string.IsNullOrEmpty(found.Email))
                    return found.Email;
            }
            catch { /* ignore */ }

            // Fallback: samAcc@berninathailand.com
            return $"{samAcc}@berninathailand.com";
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
