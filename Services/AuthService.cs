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

        /// <summary>
        /// รับ SSO callback params → สร้าง UserSession
        /// พร้อมดึง signature image จาก bt_digitalsign
        /// </summary>
        public async Task<UserSessionModel?> BuildSessionFromSsoAsync(
            string ssoId, string adUser, string email, string fname, string depart)
        {
            // DOMAIN\SAKULCHAI.P → sakulchai.p
            var samAcc = adUser.Contains('\\')
                ? adUser.Split('\\').Last().ToLower()
                : adUser.ToLower();

            _logger.LogInformation("SSO Build Session: sam={sam} dept={dept}", samAcc, depart);

            // ดึง HR data และ signature image พร้อมกัน
            var hrTask = GetHRUserAsync(samAcc);
            var sigTask = _signService.GetSignatureImageAsync(samAcc);
            await Task.WhenAll(hrTask, sigTask);

            var hrUser = hrTask.Result;
            var signatureBase64 = sigTask.Result ?? string.Empty;

            if (!string.IsNullOrEmpty(signatureBase64))
                _logger.LogInformation("Signature image loaded for {sam}", samAcc);
            else
                _logger.LogInformation("No signature image registered for {sam} — user should register at bt_signature", samAcc);

            return new UserSessionModel
            {
                SsoId = ssoId,
                SamAcc = samAcc,
                FullName = fname,
                Email = string.IsNullOrEmpty(email) ? hrUser?.user_email ?? "" : email,
                Department = depart,
                DeptManagerSam = hrUser?.samacc_depmgr ?? string.Empty,
                DeptManagerEmail = hrUser?.depmgr_email ?? string.Empty,
                EmpCode = hrUser?.emp_code ?? string.Empty,
                SignatureImageBase64 = signatureBase64,
                Role = DetermineRole(samAcc, hrUser)
            };
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

        // TODO: ปรับตาม org chart จริง (AD Group / ITPO_UserRoles table)
        private static string DetermineRole(string samAcc, HRUserModel? hr) => "User";
    }
}
