namespace BTITPORequest.Models
{
    // ── Employee from ITPO_GetEmployeelist SP ────────────────
    public class EmployeeModel
    {
        public Guid USERID { get; set; }
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

        // Joined from ITPO_UserRoles — 1 คนมีได้หลาย Role
        public List<string> AssignedRoles { get; set; } = new();

        // Priority role: Admin > Approver > Issuer > null
        public string? AssignedRole =>
            AssignedRoles.Contains("Admin")    ? "Admin"    :
            AssignedRoles.Contains("Approver") ? "Approver" :
            AssignedRoles.Contains("Issuer")   ? "Issuer"   : null;

        public bool IsIssuer   => AssignedRoles.Contains("Issuer");
        public bool IsApprover => AssignedRoles.Contains("Approver");
        public bool IsAdmin    => AssignedRoles.Contains("Admin");
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
        public string RoleName { get; set; } = string.Empty;   // Issuer | Approver | Admin
    }

    // ── Remove Specific Role Request ─────────────────────────
    public class RemoveRoleRequest
    {
        public string SamAcc { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;   // Role ที่ต้องการลบ
    }

    // ── Email Log ────────────────────────────────────────────
    public class EmailLogModel
    {
        public int      LogId       { get; set; }
        public DateTime SentAt      { get; set; }
        public string   ToEmail     { get; set; } = string.Empty;
        public string   Subject     { get; set; } = string.Empty;
        public string?  PONumber    { get; set; }
        public int?     POId        { get; set; }
        public string?  MailType    { get; set; }
        public bool     IsSuccess   { get; set; }
        public int?     HttpStatus  { get; set; }
        public string?  ErrorMsg    { get; set; }
        public bool     IsDebug     { get; set; }
        public string?  OriginalTo  { get; set; }
        public string?  CreatedBy   { get; set; }
    }

    public class EmailLogViewModel
    {
        public List<EmailLogModel> Logs       { get; set; } = new();
        public int                 TotalCount { get; set; }
        public int                 PageNum    { get; set; } = 1;
        public int                 PageSize   { get; set; } = 50;
        public int                 TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public DateTime? DateFrom  { get; set; }
        public DateTime? DateTo    { get; set; }
        public string?   PONumber  { get; set; }
        public string?   MailType  { get; set; }
        public bool?     IsSuccess { get; set; }
    }

    public class InsertEmailLogModel
    {
        public string  ToEmail    { get; set; } = string.Empty;
        public string  Subject    { get; set; } = string.Empty;
        public string? PONumber   { get; set; }
        public int?    POId       { get; set; }
        public string? MailType   { get; set; }
        public bool    IsSuccess  { get; set; }
        public int?    HttpStatus { get; set; }
        public string? ErrorMsg   { get; set; }
        public bool    IsDebug    { get; set; }
        public string? OriginalTo { get; set; }
        public string? CreatedBy  { get; set; }
    }
}
