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

        // JWT token cache
        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);

        public DigitalSignService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<DigitalSignService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        // ── Shared: API Key header ────────────────────────────
        private string ApiKey => _config["DigitalSignApi:ApiKey"] ?? "";

        private System.Net.Http.HttpClient CreateClientWithApiKey()
        {
            var client = _httpClientFactory.CreateClient("DigitalSign");
            if (!string.IsNullOrEmpty(ApiKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", ApiKey);
            return client;
        }

        // ── Health Check ──────────────────────────────────────
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
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

        // ── Get JWT Token ─────────────────────────────────────
        // POST /api/auth/token  (username + password)
        public async Task<string?> GetTokenAsync()
        {
            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                    return _cachedToken;

                var dsConfig = _config.GetSection("DigitalSignApi");
                var client = CreateClientWithApiKey();

                var payload = new { username = dsConfig["Username"], password = dsConfig["Password"] };
                var content = new StringContent(
                    JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/auth/token", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("DigitalSign token failed: {status}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DsApiResponse<DsTokenResult>>(json);
                if (result?.Success != true || result.Data == null) return null;

                _cachedToken = result.Data.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(450); // cache 7.5 hrs
                return _cachedToken;
            }
            finally { _tokenLock.Release(); }
        }

        // ── Sign Data ─────────────────────────────────────────
        // POST /api/sign  (requires JWT Bearer)
        public async Task<DsSignResult?> SignDataAsync(
            string referenceId, string purpose,
            string signerUsername, string signerFullName,
            string? department = null, string? remarks = null)
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new DsSignResult { IsSuccess = false, ErrorMessage = "Cannot get signing token" };

                var client = CreateClientWithApiKey();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var request = new DsSignRequest
                {
                    DataToSign = $"IT Purchase Order {referenceId} — {purpose} by {signerFullName}",
                    ReferenceId = referenceId,
                    Purpose = purpose,
                    Department = department,
                    Remarks = remarks ?? $"IT PO {purpose}"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/sign", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SignData failed {status}: {json}", response.StatusCode, json);
                    return new DsSignResult { IsSuccess = false, ErrorMessage = $"HTTP {response.StatusCode}" };
                }

                var result = JsonConvert.DeserializeObject<DsApiResponse<DsSignResult>>(json);
                if (result?.Data != null) result.Data.IsSuccess = result.Success;
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignDataAsync error. Ref={ref}", referenceId);
                return new DsSignResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        // ── Sign PDF ──────────────────────────────────────────
        // POST /api/pdf/sign  (requires JWT Bearer)
        public async Task<byte[]?> SignPdfAsync(
            byte[] pdfBytes, string documentName, string referenceId,
            string signerUsername, string signerFullName, string signerRole,
            int signPage = 1, float x = 36f, float y = 36f,
            float width = 200f, float height = 60f)
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("SignPdf: no token, returning unsigned PDF");
                    return pdfBytes;
                }

                var dsConfig = _config.GetSection("DigitalSignApi");
                var client = CreateClientWithApiKey();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                client.Timeout = TimeSpan.FromSeconds(60);

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
                    WebSource = dsConfig["WebSource"],
                    DocumentType = dsConfig["DocumentType"],
                    SignaturePage = signPage,
                    SignatureX = x, SignatureY = y,
                    SignatureWidth = width, SignatureHeight = height
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/pdf/sign", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SignPdf failed {status}: {json}", response.StatusCode, json);
                    return pdfBytes; // fallback unsigned
                }

                var result = JsonConvert.DeserializeObject<DsApiResponse<DsPdfSignResult>>(json);
                if (result?.Success == true && !string.IsNullOrEmpty(result.Data?.PdfBase64))
                    return Convert.FromBase64String(result.Data.PdfBase64);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignPdfAsync error. Ref={ref}", referenceId);
                return pdfBytes;
            }
        }

        // ── Get Signature Image ───────────────────────────────
        // GET /api/signature-registry/image/{samAccount}
        // Header: X-Api-Key = InternalApiKey (ApiKey ใน appsettings.json)
        public async Task<string?> GetSignatureImageAsync(string samAcc)
        {
            if (string.IsNullOrWhiteSpace(samAcc)) return null;
            var sam = samAcc.ToLower().Trim();

            try
            {
                var client = CreateClientWithApiKey();
                // endpoint ดึง binary image โดยตรง
                var response = await client.GetAsync($"/api/signature-registry/image/{sam}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "GetSignatureImage: {sam} returned {status} (no signature registered yet?)",
                        sam, response.StatusCode);
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0) return null;

                _logger.LogInformation("Got signature image for {sam} ({bytes} bytes)", sam, bytes.Length);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetSignatureImageAsync error for {sam}", sam);
                return null;
            }
        }
    }
}
