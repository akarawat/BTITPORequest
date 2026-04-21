using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BTITPORequest.Controllers
{
    /// <summary>
    /// Email notification service — registered as Scoped DI (ไม่ใช่ MVC Controller)
    /// </summary>
    public class SendMailController
    {
        private readonly IConfiguration _config;

        // ── appsettings.json keys (เหมือน BTQCDar) ──────────────────────────
        private string MailApiUrl => _config["TBCorApiServices:EmailSender"] ?? string.Empty;
        private string MailForm => _config["TBCorApiServices:MailForm"] ?? string.Empty;
        private string BearerToken => _config["TBCorApiServices:MailToken"] ?? "-dev_token-";
        private string SiteUrl => _config["AppSettings:MainAppBaseUrl"] ?? "https://it_porequest.berninathailand.com";

        // Debug redirect
        private bool IsDebug => _config.GetValue<bool>("MailSettings:DebugMode", false);
        private string DebugEmail => _config["MailSettings:DebugEmail"] ?? string.Empty;

        public SendMailController(IConfiguration config)
        {
            _config = config;
        }

        // ── Core send ─────────────────────────────────────────────────────────
        public async Task<bool> SendAsync(string toEmail, string subject,
                                          string htmlBody, string? ccEmail = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;
            if (string.IsNullOrWhiteSpace(MailApiUrl))
            {
                Console.Error.WriteLine("[SendMail] TBCorApiServices:EmailSender is not configured.");
                return false;
            }

            string finalTo = IsDebug && !string.IsNullOrWhiteSpace(DebugEmail) ? DebugEmail : toEmail;
            string finalSubject = IsDebug ? $"[DEBUG → {toEmail}] {subject}" : subject;

            Console.WriteLine(IsDebug
                ? $"[SendMail:DEBUG] {toEmail} → {finalTo} | {subject}"
                : $"[SendMail] → {finalTo} | {subject}");

            try
            {
                var param = new
                {
                    body = htmlBody,
                    form = MailForm,
                    subject = finalSubject,
                    addresses = finalTo,
                    priority = 1
                };

                var json = JsonSerializer.Serialize(param);
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
                var body = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[SendMail] Status={(int)response.StatusCode} " +
                                  $"Response={body.Substring(0, Math.Min(200, body.Length))}");

                if (!response.IsSuccessStatusCode)
                    Console.Error.WriteLine($"[SendMail] FAILED {(int)response.StatusCode} to {finalTo}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SendMail] Exception → {finalTo}: {ex.Message}");
                return false;
            }
        }

        // ── PO Notification Templates ─────────────────────────────────────────

        /// <summary>
        /// [Step 1] Requester กด Submit → แจ้ง Issuer ให้ Issue PO
        /// </summary>
        public Task<bool> NotifyIssuerAsync(
            string issuerEmail, string poNumber, string vendorCompany,
            string subject, string requesterName, string department,
            string grandTotal, int poId)
            => SendAsync(
                issuerEmail,
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
                $"{SiteUrl}/PORequest/Detail/{poId}",
                "Issue Document",
                "You are assigned as the Issuer for this IT Purchase Order. Please review and issue."));

        /// <summary>
        /// [Step 2] Issuer กด Issue → แจ้ง Approver ให้ Authorize PO
        /// </summary>
        public Task<bool> NotifyApproverAsync(
            string approverEmail, string poNumber, string vendorCompany,
            string subject, string issuerName, string grandTotal, int poId)
            => SendAsync(
                approverEmail,
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
                $"{SiteUrl}/PORequest/Detail/{poId}",
                "Authorize Document",
                "You are assigned as the Approver for this IT Purchase Order. Please review and authorize."));

        /// <summary>
        /// [Step 3] Approver กด Approve → แจ้ง Requester ว่า PO Completed
        /// </summary>
        public Task<bool> NotifyCompletedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string approverName, string grandTotal, int poId)
            => SendAsync(
                requesterEmail,
                $"[IT PO] Approved & Completed — {poNumber}",
                Build("Your PO Has Been Fully Approved", ("success", "Completed"),
                new[]
                {
                    ("PO Number",   poNumber),
                    ("Vendor",      vendorCompany),
                    ("Subject",     subject),
                    ("Approved By", approverName),
                    ("Grand Total", $"฿ {grandTotal}"),
                    ("Date",        DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                },
                $"{SiteUrl}/PORequest/Detail/{poId}",
                "Download PDF",
                "Your IT Purchase Order has been fully approved. You can now download the signed PDF."));

        /// <summary>
        /// Approver กด Reject → แจ้ง Requester ว่าถูก Reject
        /// </summary>
        public Task<bool> NotifyRejectedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string rejectedByName, string remarks,
            int poId, int level)
            => SendAsync(
                requesterEmail,
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
                $"{SiteUrl}/PORequest/Detail/{poId}",
                "Revise & Resubmit",
                "Please review the rejection remarks and revise your purchase order if necessary."));

        /// <summary>
        /// แจ้ง Requester ว่า PO ถูก Issue แล้ว (ทราบความคืบหน้า)
        /// </summary>
        public Task<bool> NotifyRequesterIssuedAsync(
            string requesterEmail, string poNumber, string vendorCompany,
            string subject, string issuerName, string grandTotal, int poId)
            => SendAsync(
                requesterEmail,
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
                $"{SiteUrl}/PORequest/Detail/{poId}",
                "View PO",
                "Your IT Purchase Order has been issued and is now pending final authorization."));


        // ── HTML builder (เหมือน BTQCDar ทุกอย่าง) ──────────────────────────
        private static string Build(string title, (string color, string label) badge,
            (string label, string value)[] rows, string actionUrl,
            string actionLabel, string footer)
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
                ["warning"] = "#ffc107",
                ["info"] = "#0dcaf0",
                ["primary"] = "#0d6efd",
                ["success"] = "#198754",
                ["danger"] = "#dc3545"
            };

            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:32px 16px;'>
<table width='620' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);'>

  <!-- Header -->
  <tr><td style='background:#1a5676;padding:22px 32px;'>
    <span style='color:#fff;font-size:20px;font-weight:bold;'>
      &#128196; BERNINA Thailand — IT Purchase Order
    </span>
  </td></tr>

  <!-- Body -->
  <tr><td style='padding:28px 32px 24px;'>
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

  <!-- Footer -->
  <tr><td style='padding:14px 32px;border-top:1px solid #f0f0f0;color:#aaa;font-size:11px;line-height:1.6;'>
    {System.Net.WebUtility.HtmlEncode(footer)}<br>
    This is an automated notification from BTITPORequest. Please do not reply.
  </td></tr>

</table></td></tr></table>
</body></html>";
        }
    }
}
