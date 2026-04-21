using BTITPORequest.Data;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Dapper;

namespace BTITPORequest.Services
{
    public class AuthService : IAuthService
    {
        private readonly DbContext _db;
        private readonly IDigitalSignService _signService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(DbContext db, IDigitalSignService signService, ILogger<AuthService> logger)
        {
            _db = db;
            _signService = signService;
            _logger = logger;
        }

        public async Task<UserSessionModel?> BuildSessionFromSsoAsync(
            string ssoId, string adUser, string email, string fname, string depart)
        {
            var samAcc = adUser.Contains('\\')
                ? adUser.Split('\\').Last().ToLower()
                : adUser.ToLower();

            _logger.LogInformation("SSO: building session for sam={sam}", samAcc);

            HRUserModel? hrUser = null;
            string? sigBase64 = null;
            string role = "User";

            // ── Role: ดึงก่อนแยกต่างหาก — fast query, ไม่ควรถูก cancel ──
            role = await GetUserRoleSafeAsync(samAcc);

            // ── HR + Signature: ดึงพร้อมกัน timeout 5 วินาที ─────────────
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var hrTask = GetHRUserSafeAsync(samAcc, cts.Token);
                var sigTask = GetSignatureSafeAsync(samAcc, cts.Token);
                await Task.WhenAll(hrTask, sigTask);
                hrUser = hrTask.Result;
                sigBase64 = sigTask.Result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HR/Signature fetch timeout for {sam}", samAcc);
            }

            var session = new UserSessionModel
            {
                SsoId = ssoId,
                SamAcc = samAcc,
                FullName = fname,
                Email = string.IsNullOrEmpty(email) ? hrUser?.user_email ?? "" : email,
                Department = depart,
                DeptManagerSam = hrUser?.samacc_depmgr ?? string.Empty,
                DeptManagerEmail = hrUser?.depmgr_email ?? string.Empty,
                EmpCode = hrUser?.emp_code ?? string.Empty,
                SignatureImageBase64 = sigBase64 ?? string.Empty,
                Role = role
            };

            _logger.LogInformation("Session built: {sam} role={role}", samAcc, role);
            return session;
        }

        // ── Get role — ไม่ใช้ CancellationToken เพราะเป็น fast local query ──
        private async Task<string> GetUserRoleSafeAsync(string samAcc)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                var role = await conn.ExecuteScalarAsync<string>(
                    "ITPO_sp_GetUserRole",
                    new { SamAcc = samAcc },
                    commandType: System.Data.CommandType.StoredProcedure);
                _logger.LogInformation("GetUserRole: {sam} = {role}", samAcc, role ?? "User");
                return role ?? "User";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetUserRole failed for {sam}", samAcc);
                return "User";
            }
        }

        private async Task<HRUserModel?> GetHRUserSafeAsync(string samAcc, CancellationToken ct)
        {
            try
            {
                using var conn = _db.GetBT_HRConnection();
                var cmd = new CommandDefinition(
                    "sp_ITPOgetAllSamUser",
                    commandType: System.Data.CommandType.StoredProcedure,
                    cancellationToken: ct);
                var all = await conn.QueryAsync<HRUserModel>(cmd);
                return all.FirstOrDefault(u =>
                    u.samacc.Equals(samAcc, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetHRUser failed for {sam}", samAcc);
                return null;
            }
        }

        private async Task<string?> GetSignatureSafeAsync(string samAcc, CancellationToken ct)
        {
            try
            {
                var task = _signService.GetSignatureImageAsync(samAcc);
                var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, ct));
                return completed == task ? await task : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetSignature failed for {sam}", samAcc);
                return null;
            }
        }

        public async Task<HRUserModel?> GetHRUserAsync(string samAcc)
        {
            try
            {
                using var conn = _db.GetBT_HRConnection();
                var all = await conn.QueryAsync<HRUserModel>(
                    "sp_ITPOgetAllSamUser",
                    commandType: System.Data.CommandType.StoredProcedure);
                return all.FirstOrDefault(u =>
                    u.samacc.Equals(samAcc, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHRUserAsync failed for {sam}", samAcc);
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
                _logger.LogError(ex, "GetAllUsersAsync failed");
                return new List<HRUserModel>();
            }
        }
    }
}
