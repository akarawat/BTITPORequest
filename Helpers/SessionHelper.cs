using BTITPORequest.Models;
using Newtonsoft.Json;
using System.Security.Claims;

namespace BTITPORequest.Helpers
{
    public static class SessionHelper
    {
        private const string UserKey = "CurrentUser";

        public static void SetUser(ISession session, UserSessionModel user)
            => session.SetString(UserKey, JsonConvert.SerializeObject(user));

        public static UserSessionModel? GetUser(ISession session)
        {
            var json = session.GetString(UserKey);
            return json == null ? null : JsonConvert.DeserializeObject<UserSessionModel>(json);
        }

        public static void ClearUser(ISession session) => session.Remove(UserKey);

        /// <summary>สร้าง UserSessionModel จาก ClaimsPrincipal (cookie auth)</summary>
        public static UserSessionModel? GetUserFromClaims(ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true) return null;
            return new UserSessionModel
            {
                SsoId = principal.FindFirstValue("sso_id") ?? "",
                SamAcc = principal.FindFirstValue("samacc") ?? "",
                EmpCode = principal.FindFirstValue("emp_code") ?? "",
                FullName = principal.FindFirstValue(ClaimTypes.Name) ?? "",
                Email = principal.FindFirstValue(ClaimTypes.Email) ?? "",
                Department = principal.FindFirstValue("department") ?? "",
                DeptManagerSam = principal.FindFirstValue("depmgr_sam") ?? "",
                SignatureImageBase64 = principal.FindFirstValue("signature_b64") ?? "",
                Role = principal.FindFirstValue(ClaimTypes.Role) ?? "User"
            };
        }
    }
}
