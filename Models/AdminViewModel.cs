namespace BTITPORequest.Models
{
    // ── Employee from ITPO_GetEmployeelist SP ────────────────
    public class EmployeeModel
    {
        public string USERID { get; set; } = string.Empty;
        public string DISPNAME { get; set; } = string.Empty;
        public string DISPNAME_TH { get; set; } = string.Empty;
        public string UEMAIL { get; set; } = string.Empty;
        public string DEPART { get; set; } = string.Empty;
        public string SAMACC { get; set; } = string.Empty;
        public string DESCRIP { get; set; } = string.Empty;     // ตำแหน่ง
        public string reporter { get; set; } = string.Empty;    // Manager name
        public string mgr_emp_code { get; set; } = string.Empty;
        public string mgr_email { get; set; } = string.Empty;
        public string mgr_samacc { get; set; } = string.Empty;
        public string dep_code { get; set; } = string.Empty;
        public string emp_code { get; set; } = string.Empty;
        public DateTime? CREATE_DT { get; set; }

        // Joined from ITPO_UserRoles (not in SP result — filled in service)
        public string? AssignedRole { get; set; }       // null = ไม่มีสิทธิ์พิเศษ
        public bool IsIssuer   => AssignedRole == "Issuer";
        public bool IsApprover => AssignedRole == "Approver";
        public bool IsAdmin    => AssignedRole == "Admin";
    }

    // ── Role assignment record ───────────────────────────────
    public class UserRoleModel
    {
        public int RoleId { get; set; }
        public string SamAcc { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ── Admin Index ViewModel ────────────────────────────────
    public class AdminIndexViewModel
    {
        public List<EmployeeModel> Employees { get; set; } = new();
        public List<UserRoleModel> CurrentRoles { get; set; } = new();
        public string? SearchText { get; set; }
        public string? FilterDept { get; set; }
        public List<string> Departments { get; set; } = new();
        public int TotalEmployees { get; set; }
        public int IssuerCount { get; set; }
        public int ApproverCount { get; set; }
        public int AdminCount { get; set; }
    }

    // ── Upsert Role Request ──────────────────────────────────
    public class UpsertRoleRequest
    {
        public string SamAcc { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;   // Issuer | Approver | Admin | (empty=remove)
    }
}
