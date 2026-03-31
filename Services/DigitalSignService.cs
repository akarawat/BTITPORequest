using BTITPORequest.Services.Interfaces;
using Newtonsoft.Json;

namespace BTITPORequest.Services
{
    public class DigitalSignService : IDigitalSignService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DigitalSignService> _logger;

        public DigitalSignService(IHttpClientFactory httpClientFactory, ILogger<DigitalSignService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DigitalSign");
                var response = await client.GetAsync("/api/certificate/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string? Token, string? UserInfo)> AuthenticateAsync(string username, string password)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DigitalSign");
                var payload = new { username, password };
                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/account/login", content);
                if (!response.IsSuccessStatusCode) return (false, null, null);

                var json = await response.Content.ReadAsStringAsync();
                dynamic? result = JsonConvert.DeserializeObject(json);
                string token = result?.token ?? result?.access_token ?? "";

                return (true, token, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthenticateAsync error");
                return (false, null, null);
            }
        }

        /// <summary>
        /// Request (Requester) signature - step 1 of the flow
        /// </summary>
        public async Task<(bool Success, string? SignatureUrl, string? Error)> RequestSignatureAsync(
            string token, int poId, string documentType)
        {
            return await CallSignApiAsync(token, poId, documentType, "request");
        }

        /// <summary>
        /// Approve signature - for Approver steps
        /// </summary>
        public async Task<(bool Success, string? SignatureUrl, string? Error)> ApproveSignatureAsync(
            string token, int poId, string documentType)
        {
            return await CallSignApiAsync(token, poId, documentType, "approve");
        }

        private async Task<(bool Success, string? SignatureUrl, string? Error)> CallSignApiAsync(
            string token, int poId, string documentType, string signType)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DigitalSign");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var payload = new
                {
                    documentId = poId.ToString(),
                    documentType = documentType,
                    signType = signType,
                    callbackUrl = $"https://it_porequest.berninathailand.com/PORequest/SignCallback"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/sign/create", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, null, json);

                dynamic? result = JsonConvert.DeserializeObject(json);
                string signUrl = result?.signatureUrl ?? result?.url ?? result?.data?.signatureUrl ?? "";

                return (true, signUrl, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CallSignApiAsync error for PO {poId}", poId);
                return (false, null, ex.Message);
            }
        }
    }
}
