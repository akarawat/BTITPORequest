using BTITPORequest.Data;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Dapper;
using Newtonsoft.Json;

namespace BTITPORequest.Services
{
    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IHttpClientFactory httpClientFactory, DbContext db, IConfiguration config, ILogger<AuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _db = db;
            _config = config;
            _logger = logger;
        }

        public async Task<(bool Success, UserSessionModel? User, string ErrorMessage)> LoginAsync(string username, string password)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DigitalSign");

                var payload = new { username, password };
                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                // Call Digital Sign API login
                var response = await client.PostAsync("/api/account/login", content);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Login failed for {user}: {err}", username, err);
                    return (false, null, "Invalid username or password.");
                }

                var responseText = await response.Content.ReadAsStringAsync();
                dynamic? result = JsonConvert.DeserializeObject(responseText);

                string token = result?.token ?? result?.access_token ?? "";
                if (string.IsNullOrEmpty(token))
                    return (false, null, "Authentication failed: No token received.");

                // Get HR user info
                var hrUser = await GetHRUserAsync(username);
                if (hrUser == null)
                    return (false, null, "User not found in HR system.");

                var userSession = new UserSessionModel
                {
                    SamAcc = hrUser.samacc,
                    EmpCode = hrUser.emp_code,
                    FullName = hrUser.fName,
                    Email = hrUser.user_email,
                    DeptManagerSam = hrUser.samacc_depmgr,
                    DeptManagerEmail = hrUser.depmgr_email,
                    Token = token,
                    Role = DetermineRole(hrUser)
                };

                return (true, userSession, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {user}", username);
                return (false, null, $"Login error: {ex.Message}");
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DigitalSign");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync("/api/certificate/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<HRUserModel?> GetHRUserAsync(string samAcc)
        {
            try
            {
                using var conn = _db.GetBT_HRConnection();
                var result = await conn.QueryAsync<HRUserModel>(
                    "sp_ITPOgetAllSamUser",
                    commandType: System.Data.CommandType.StoredProcedure);

                return result.FirstOrDefault(u =>
                    u.samacc.Equals(samAcc, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHRUserAsync error for {sam}", samAcc);
                return null;
            }
        }

        public async Task<List<HRUserModel>> GetAllUsersAsync()
        {
            try
            {
                using var conn = _db.GetBT_HRConnection();
                var result = await conn.QueryAsync<HRUserModel>(
                    "sp_ITPOgetAllSamUser",
                    commandType: System.Data.CommandType.StoredProcedure);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllUsersAsync error");
                return new List<HRUserModel>();
            }
        }

        private static string DetermineRole(HRUserModel user)
        {
            // Role determination logic - customize based on your business rules
            // e.g., check if user is a department manager
            if (!string.IsNullOrEmpty(user.samacc_depmgr))
                return "User";

            return "User";
        }
    }
}
