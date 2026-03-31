namespace BTITPORequest.Models
{
    public class UserSessionModel
    {
        public string SamAcc { get; set; } = string.Empty;
        public string EmpCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DeptManagerSam { get; set; } = string.Empty;
        public string DeptManagerEmail { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;     // JWT from DigitalSign API
        public string Role { get; set; } = "User";            // User | Approver | Admin
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
