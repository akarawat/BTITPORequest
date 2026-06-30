using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;

namespace BTITPORequest.Controllers
{
    /// <summary>
    /// Email notification service — registered as Scoped DI (ไม่ใช่ MVC Controller)
    /// </summary>
    public class SendMailController
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SendMailController> _logger;
        private readonly IPOService _poService;

        private string MailApiUrl  => _config["TBCorApiServices:EmailSender"] ?? string.Empty;
        private string MailForm    => _config["TBCorApiServices:MailForm"] ?? string.Empty;
        private string BearerToken => _config["TBCorApiServices:MailToken"] ?? "-dev_token-";
        private string SiteUrl     => _config["AppSettings:MainAppBaseUrl"] ?? "https://it_porequest.berninathailand.com";
        private bool   IsDebug     => _config.GetValue<bool>("MailSettings:DebugMode", false);
        private string DebugEmail  => _config["MailSettings:DebugEmail"] ?? string.Empty;

        private static string LogDir => System.IO.Path.Combine(
            Directory.GetCurrentDirectory(), "logs", "email");

        public SendMailController(IConfiguration config, ILogger<SendMailController> logger,
            IPOService poService)
        {
            _config    = config;
            _logger    = logger;
            _poService = poService;
        }

        // ── Core send ─────────────────────────────────────────────────────────
        public async Task<bool> SendAsync(string toEmail, string subject,
            string htmlBody, string? ccEmail = null,
            string? mailType = null, string? poNumber = null, int? poId = null,
            string? createdBy = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            bool   isDebug     = IsDebug && !string.IsNullOrWhiteSpace(DebugEmail);
            string finalTo     = isDebug ? DebugEmail : toEmail;
            string finalSubject = isDebug ? $"[DEBUG → {toEmail}] {subject}" : subject;

            WriteLog($"SENDING | To: {finalTo} | Subject: {finalSubject}" +
                     (isDebug ? $" | [DEBUG, original: {toEmail}]" : ""));

            if (string.IsNullOrWhiteSpace(MailApiUrl))
            {
                WriteLog($"SKIPPED | TBCorApiServices:EmailSender not configured");
                _ = _poService.LogEmailAsync(new InsertEmailLogModel
                {
                    ToEmail = finalTo, Subject = finalSubject, PONumber = poNumber, POId = poId,
                    MailType = mailType, IsSuccess = false, ErrorMsg = "EmailSender not configured",
                    IsDebug = isDebug, OriginalTo = isDebug ? toEmail : null, CreatedBy = createdBy
                });
                return false;
            }

            int?   httpStatus = null;
            bool   success    = false;
            string errorMsg   = string.Empty;

            try
            {
                var param = new
                {
                    body = htmlBody, form = MailForm,
                    subject = finalSubject, addresses = finalTo, priority = 1
                };
                var json       = JsonSerializer.Serialize(param);
                var strContent = new StringContent(json, Encoding.UTF8, "application/json");

                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = new CookieContainer(),
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + BearerToken);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.PostAsync(MailApiUrl, strContent);
                var respBody = await response.Content.ReadAsStringAsync();
                httpStatus   = (int)response.StatusCode;
                success      = response.IsSuccessStatusCode;

                if (success)
                {
                    WriteLog($"SUCCESS | To: {finalTo} | Subject: {finalSubject} | HTTP {httpStatus}");
                    _logger.LogInformation("[SendMail] OK {s} → {to}", httpStatus, finalTo);
                }
                else
                {
                    errorMsg = respBody[..Math.Min(500, respBody.Length)];
                    WriteLog($"FAILED  | To: {finalTo} | Subject: {finalSubject} | HTTP {httpStatus} | {errorMsg}");
                    _logger.LogError("[SendMail] FAILED {s} → {to}", httpStatus, finalTo);
                }
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                WriteLog($"ERROR   | To: {finalTo} | Subject: {finalSubject} | {ex.Message}");
                _logger.LogError(ex, "[SendMail] Exception → {to}", finalTo);
            }

            // ── Log to DB (fire-and-forget) ──────────────────
            _ = _poService.LogEmailAsync(new InsertEmailLogModel
            {
                ToEmail    = finalTo,
                Subject    = finalSubject,
                PONumber   = poNumber,
                POId       = poId,
                MailType   = mailType,
                IsSuccess  = success,
                HttpStatus = httpStatus,
                ErrorMsg   = string.IsNullOrEmpty(errorMsg) ? null : errorMsg,
                IsDebug    = isDebug,
                OriginalTo = isDebug ? toEmail : null,
                CreatedBy  = createdBy
            });

            return success;
        }

        private static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var logFile = System.IO.Path.Combine(LogDir, $"email_{DateTime.Now:yyyyMMdd}.log");
                System.IO.File.AppendAllText(logFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        // ── PO Notification Templates ─────────────────────────────────────────

        public Task<bool> NotifyRequesterSubmittedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string issuerName, string grandTotal, int poId, string? createdBy = null)
            => SendAsync(requesterEmail,
                $"[IT PO] Your PO has been submitted — {poNumber}",
                Build("Your PO Has Been Submitted", ("primary", "Requested"),
                new[]
                {
                    ("PO Number",    poNumber),
                    ("Vendor",       vendorCompany),
                    ("Subject",      subject),
                    ("Forwarded To", issuerName),
                    ("Grand Total",  $"฿ {grandTotal}")
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "View PO Status",
                "Your IT Purchase Order has been submitted and is now pending issuance.",
                greeting: "Dear Requester,"),
                mailType: "Submitted", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyIssuerAsync(
            string issuerEmail, string poNumber, string vendorCompany,
            string subject, string requesterName, string department,
            string grandTotal, int poId, string? createdBy = null)
            => SendAsync(issuerEmail,
                $"[IT PO] Pending your issuance — {poNumber}",
                Build("PO Pending Your Issuance", ("warning", "Pending Issued"),
                new[]
                {
                    ("PO Number",    poNumber),
                    ("Vendor",       vendorCompany),
                    ("Subject",      subject),
                    ("Requested By", requesterName),
                    ("Department",   department),
                    ("Grand Total",  $"฿ {grandTotal}")
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "Issue Document",
                "You are assigned as the Issuer for this IT Purchase Order. Please review and issue.",
                greeting: "Dear Issuer,"),
                mailType: "Issuer", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyApproverAsync(
            string approverEmail, string poNumber, string vendorCompany,
            string subject, string issuerName, string grandTotal, int poId, string? createdBy = null)
            => SendAsync(approverEmail,
                $"[IT PO] Pending your authorization — {poNumber}",
                Build("PO Pending Your Authorization", ("info", "Pending Authorized"),
                new[]
                {
                    ("PO Number",  poNumber),
                    ("Vendor",     vendorCompany),
                    ("Subject",    subject),
                    ("Issued By",  issuerName),
                    ("Grand Total",$"฿ {grandTotal}")
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "Authorize Document",
                "You are assigned as the Approver for this IT Purchase Order. Please review and authorize.",
                greeting: "Dear Manager,"),
                mailType: "Approver", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyCompletedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string approverName, string grandTotal, int poId, string? createdBy = null)
            => SendAsync(requesterEmail,
                $"[IT PO] ✅ Approved & Completed — {poNumber}",
                Build("Your PO Has Been Approved", ("success", "✅ Completed"),
                new[]
                {
                    ("PO Number",   poNumber),
                    ("Vendor",      vendorCompany),
                    ("Subject",     subject),
                    ("Approved By", approverName),
                    ("Grand Total", $"฿ {grandTotal}"),
                    ("Date",        DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "Download PDF",
                "Your IT Purchase Order has been fully approved.",
                greeting: "Dear Requester,"),
                mailType: "Completed", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyRejectedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string rejectedByName, string remarks,
            int poId, int level, string? createdBy = null)
            => SendAsync(requesterEmail,
                $"[IT PO] Rejected — {poNumber}",
                Build("Your PO Was Not Approved", ("danger", $"Rejected (Level {level})"),
                new[]
                {
                    ("PO Number",   poNumber),
                    ("Vendor",      vendorCompany),
                    ("Subject",     subject),
                    ("Rejected By", rejectedByName),
                    ("Level",       $"Approval Level {level}"),
                    ("Reason",      string.IsNullOrEmpty(remarks) ? "—" : remarks)
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "Revise & Resubmit",
                "Please review the rejection remarks and revise your purchase order.",
                greeting: "Dear Requester,"),
                mailType: $"Rejected-L{level}", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyRequesterIssuedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string issuerName, string grandTotal, int poId, string? createdBy = null)
            => SendAsync(requesterEmail,
                $"[IT PO] Issued — {poNumber}",
                Build("Your PO Has Been Issued", ("info", "Issued"),
                new[]
                {
                    ("PO Number",  poNumber),
                    ("Vendor",     vendorCompany),
                    ("Subject",    subject),
                    ("Issued By",  issuerName),
                    ("Grand Total",$"฿ {grandTotal}")
                },
                $"{SiteUrl}/PORequest/Detail/{poId}", "View PO",
                "Your IT Purchase Order has been issued and is now pending final authorization.",
                greeting: "Dear Requester,"),
                mailType: "IssuedNotify", poNumber: poNumber, poId: poId, createdBy: createdBy);

        public Task<bool> NotifyPRClosedAsync(
            string requesterEmail, string prNumber, string closedByName,
            string? poNumber, int prId, string prRequestUrl, string? createdBy = null)
        {
            var rows = new System.Collections.Generic.List<(string, string)>
            {
                ("PR Number",  prNumber),
                ("Closed By",  closedByName),
                ("Date",       DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
            };
            if (!string.IsNullOrEmpty(poNumber))
                rows.Add(("Linked PO", poNumber));

            return SendAsync(requesterEmail,
                $"[IT PR] ✅ สินค้าถึงแล้ว — ปิด PR {prNumber}",
                Build("PR Closed — Goods Received", ("success", "✅ Goods Received"),
                rows.ToArray(),
                $"{prRequestUrl}/PRRequest/Detail/{prId}", "View PR",
                "ขออภัยในความไม่สะดวก PR ของท่านได้รับการปิดแล้ว เนื่องจากสินค้าถึงแล้ว",
                greeting: "เรียน ผู้สร้าง PR,"),
                mailType: "PRClosed", createdBy: createdBy);
        }

        // ── HTML builder ──────────────────────────────────────────────────────
        private static string Build(string title, (string color, string label) badge,
            (string label, string value)[] rows, string actionUrl,
            string actionLabel, string footer, string greeting = "Dear Manager,")
        {
            var rowHtml = new StringBuilder();
            foreach (var (lbl, val) in rows)
                rowHtml.Append($@"<tr>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;color:#555;
                             font-size:13px;white-space:nowrap;width:140px;'>
                      <strong>{lbl}</strong></td>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;font-size:13px;'>
                      {System.Net.WebUtility.HtmlEncode(val)}</td></tr>");

            var colors = new Dictionary<string, string>
            {
                ["warning"] = "#ffc107", ["info"] = "#0dcaf0",
                ["primary"] = "#0d6efd", ["success"] = "#198754", ["danger"] = "#dc3545"
            };

            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:32px 16px;'>
<table width='620' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);'>
  <tr><td style='background:#1a5676;padding:22px 32px;'>
    <span style='color:#fff;font-size:20px;font-weight:bold;'>
      &#128196; BERNINA Thailand — IT Purchase Order
    </span>
  </td></tr>
  <tr><td style='padding:28px 32px 24px;'>
    <p style='margin:0 0 16px;color:#444;font-size:14px;'>{greeting}</p>
    <p style='margin:0 0 16px;color:#555;font-size:13px;'>
        {System.Net.WebUtility.HtmlEncode(footer)}<br>The document is ready for review.</p>
    <h2 style='margin:0 0 12px;color:#1a1a1a;font-size:19px;'>{title}</h2>
    <span style='display:inline-block;padding:4px 16px;border-radius:20px;
                 background:{colors.GetValueOrDefault(badge.color, "#6c757d")};
                 color:#fff;font-size:12px;font-weight:bold;margin-bottom:22px;'>
      {badge.label}
    </span>
    <table width='100%' cellpadding='0' cellspacing='0'
           style='border:1px solid #e8e8e8;border-radius:6px;border-collapse:collapse;margin-bottom:26px;'>
      {rowHtml}
    </table>
    <a href='{actionUrl}'
       style='display:inline-block;padding:12px 30px;background:#1a5676;color:#fff;
              text-decoration:none;border-radius:6px;font-weight:bold;font-size:14px;'>
      {actionLabel} &#8594;
    </a>
  </td></tr>
  <tr><td style='padding:14px 32px;border-top:1px solid #f0f0f0;color:#aaa;font-size:12px;'>
    This is an automated notification from BTITPORequest. Please do not reply.<br>
    <span style='color:#888;font-size:12px;'>
      Best Regards,<br>
      <strong style='color:#1a5676;'>Powered by IT. Bernina Thailand.</strong>
    </span>
  </td></tr>
</table></td></tr></table>
</body></html>";
        }
    }
}
