using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Newtonsoft.Json;
using System.Text;

namespace BTITPORequest.Services
{
    public class DigitalSignService : IDigitalSignService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<DigitalSignService> _logger;

        public DigitalSignService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<DigitalSignService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        // ── สร้าง HttpClient พร้อม X-Api-Key header ──────────
        private HttpClient CreateClient(string? samAccount = null)
        {
            var client = _httpClientFactory.CreateClient("DigitalSign");
            var apiKey = _config["DigitalSignApi:ApiKey"] ?? "";
            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            if (!string.IsNullOrEmpty(samAccount))
                client.DefaultRequestHeaders.Add("X-Sam-Account", samAccount);
            return client;
        }

        // ── Health Check ──────────────────────────────────────
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                // health endpoint ไม่ต้องใช้ ApiKey
                var client = _httpClientFactory.CreateClient("DigitalSign");
                var r = await client.GetAsync("/api/certificate/health");
                return r.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DigitalSign health check failed");
                return false;
            }
        }

        // ── GetTokenAsync ไม่ใช้แล้ว (Windows SSO) ──────────
        public Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);

        // ── ดึง Signature Image ───────────────────────────────
        /// <summary>
        /// GET /api/signature-registry/user/{samAccount}
        /// ต้องส่ง X-Api-Key header
        /// คืน base64 PNG string
        /// </summary>
        public async Task<string?> GetSignatureImageAsync(string samAcc)
        {
            if (string.IsNullOrWhiteSpace(samAcc)) return null;
            var sam = samAcc.ToLower().Trim();

            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"/api/signature-registry/user/{sam}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetSignatureImage: {status} for {sam}", response.StatusCode, sam);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DsApiResponse<SignatureRegistryData>>(json);

                if (result?.Success == true && !string.IsNullOrEmpty(result.Data?.SignatureImageBase64))
                {
                    _logger.LogInformation("Got signature image for {sam}", sam);
                    return result.Data.SignatureImageBase64;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSignatureImageAsync failed for {sam}", sam);
                return null;
            }
        }

        // ── Sign Data ─────────────────────────────────────────
        /// <summary>
        /// POST /api/sign (Windows SSO — ใช้ UseDefaultCredentials)
        /// </summary>
        public async Task<DsSignResult?> SignDataAsync(
            string referenceId, string purpose,
            string signerUsername, string signerFullName,
            string? department = null, string? remarks = null)
        {
            try
            {
                // Sign endpoint ใช้ Windows SSO — สร้าง client แยก
                var handler = new HttpClientHandler { UseDefaultCredentials = true };
                using var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(_config["DigitalSignApi:BaseUrl"] ?? ""),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var request = new DsSignRequest
                {
                    DataToSign = $"PO:{referenceId} | {purpose} | {signerFullName}",
                    ReferenceId = referenceId,
                    Purpose = purpose,
                    Department = department,
                    Remarks = remarks ?? $"IT Purchase Order — {purpose}"
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/sign", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SignData {status}: {json}", response.StatusCode, json);
                    return new DsSignResult { IsSuccess = false, ErrorMessage = $"HTTP {response.StatusCode}" };
                }

                var result = JsonConvert.DeserializeObject<DsApiResponse<DsSignResult>>(json);
                if (result?.Data != null) result.Data.IsSuccess = result.Success;
                _logger.LogInformation("Signed PO {ref} — {purpose} by {user}", referenceId, purpose, signerUsername);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignDataAsync failed. Ref={ref}", referenceId);
                return new DsSignResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        // ── Sign PDF ──────────────────────────────────────────
        /// <summary>
        /// POST /api/pdf/sign (Windows SSO)
        /// ส่ง SignerUsername เพื่อ override double-hop identity
        /// </summary>
        public async Task<byte[]?> SignPdfAsync(
            byte[] pdfBytes, string documentName, string referenceId,
            string signerUsername, string signerFullName,
            string signerRole, int signPage = 1,
            float x = 36f, float y = 36f, float width = 200f, float height = 60f)
        {
            try
            {
                var handler = new HttpClientHandler { UseDefaultCredentials = true };
                using var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(_config["DigitalSignApi:BaseUrl"] ?? ""),
                    Timeout = TimeSpan.FromSeconds(60)
                };
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var request = new DsPdfSignRequest
                {
                    PdfBase64 = Convert.ToBase64String(pdfBytes),
                    DocumentName = documentName,
                    ReferenceId = referenceId,
                    Reason = "Approved — IT Purchase Order",
                    Location = "Lamphun, Thailand",
                    SignerUsername = signerUsername,
                    SignerFullName = signerFullName,
                    SignerRole = signerRole,
                    WebSource = _config["DigitalSignApi:WebSource"],
                    DocumentType = _config["DigitalSignApi:DocumentType"],
                    SignaturePage = signPage,
                    SignatureX = x, SignatureY = y,
                    SignatureWidth = width, SignatureHeight = height
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/pdf/sign", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SignPdf {status}: {json}", response.StatusCode, json);
                    return pdfBytes; // fallback unsigned
                }

                var result = JsonConvert.DeserializeObject<DsApiResponse<DsPdfSignResult>>(json);
                if (result?.Success == true && !string.IsNullOrEmpty(result.Data?.PdfBase64))
                {
                    _logger.LogInformation("PDF signed. Ref={ref} by {user}", referenceId, signerUsername);
                    return Convert.FromBase64String(result.Data.PdfBase64);
                }

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignPdfAsync failed. Ref={ref}", referenceId);
                return pdfBytes;
            }
        }
    }

    // ── DTO สำหรับ /api/signature-registry/user/{sam} ─────────
    public class SignatureRegistryData
    {
        public string SamAccountName { get; set; } = string.Empty;
        public string FullNameEN { get; set; } = string.Empty;
        public string FullNameTH { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public string SignatureImageBase64 { get; set; } = string.Empty;
    }
}
