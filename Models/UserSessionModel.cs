namespace BTITPORequest.Models
{
    public class UserSessionModel
    {
        // SSO fields — ได้มาจาก btauthen callback query string
        public string SamAcc { get; set; } = string.Empty;        // เช่น "sakulchai.p"
        public string EmpCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;       // fname
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;    // depart
        public string SsoId { get; set; } = string.Empty;         // id (GUID จาก SSO)

        // HR DB fields
        public string DeptManagerSam { get; set; } = string.Empty;
        public string DeptManagerEmail { get; set; } = string.Empty;

        // Signature image (base64 PNG จาก bt_digitalsign)
        public string SignatureImageBase64 { get; set; } = string.Empty;

        public string Role { get; set; } = "User"; // User | Issuer | Approver | Admin
    }

    public class HRUserModel
    {
        public string samacc { get; set; } = string.Empty;
        public string emp_code { get; set; } = string.Empty;
        public string fName { get; set; } = string.Empty;
        public string user_email { get; set; } = string.Empty;
        public string samacc_depmgr { get; set; } = string.Empty;
        public string depmgr_email { get; set; } = string.Empty;
    }
}
