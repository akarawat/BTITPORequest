namespace BTITPORequest.Models
{
    /// <summary>
    /// PR header summary — ใช้แสดงในหน้า "New PO from PR" (list view)
    /// ดึงจาก ITPR_PurchaseRequisitions ผ่าน ITPR_sp_GetAuthorizedPRsForPO
    /// </summary>
    public class PRForPOModel
    {
        public int      PRId            { get; set; }
        public string   PRNumber        { get; set; } = string.Empty;
        public string   ReasonToOrder   { get; set; } = string.Empty;
        public string   RequesterSam    { get; set; } = string.Empty;
        public string   RequesterName   { get; set; } = string.Empty;
        public string   SupplierCompany { get; set; } = string.Empty;
        public string   SupplierVC      { get; set; } = string.Empty;
        public string   SupplierContact { get; set; } = string.Empty;
        public string   SupplierEmail   { get; set; } = string.Empty;
        public string   SupplierTel     { get; set; } = string.Empty;
        public string   SupplierFax     { get; set; } = string.Empty;
        public string   SupplierQuoteRef{ get; set; } = string.Empty;
        public decimal  GrandTotal      { get; set; }
        public int?     LinkedDeptId    { get; set; }
        public string   LinkedDeptName  { get; set; } = string.Empty;
        public string?  LinkedPONumber  { get; set; }   // null = ยังไม่ถูกเปิด PO
        public DateTime CreatedAt       { get; set; }
        public DateTime UpdatedAt       { get; set; }

        public bool HasLinkedPO => !string.IsNullOrEmpty(LinkedPONumber);
    }

    /// <summary>
    /// PR line item — ใช้ pre-fill ตาราง Line Items ใน PO Create form
    /// ดึงจาก ITPR_PRLineItems ผ่าน ITPR_sp_GetPRLineItemsForPO
    /// </summary>
    public class PRLineItemForPOModel
    {
        public int     ItemId      { get; set; }
        public int     PRId        { get; set; }
        public int     LineNo      { get; set; }
        public string  BTNumber    { get; set; } = string.Empty;
        public string  Description { get; set; } = string.Empty;
        public string  BrandModel  { get; set; } = string.Empty;
        public string  AccountCode { get; set; } = string.Empty;
        public string  CostDept    { get; set; } = string.Empty;
        public decimal Quantity    { get; set; } = 1;
        public string  Unit        { get; set; } = string.Empty;
        public decimal UnitPrice   { get; set; }
        public decimal Amount      { get; set; }
    }

    /// <summary>
    /// PR ที่ถูกปิดแล้ว (Status=8) — ใช้แสดงใน Audit tab
    /// ดึงจาก ITPR_sp_GetClosedPRsForAudit
    /// </summary>
    public class ClosedPRModel
    {
        public int      PRId          { get; set; }
        public string   PRNumber      { get; set; } = string.Empty;
        public string   ReasonToOrder { get; set; } = string.Empty;
        public string   RequesterName { get; set; } = string.Empty;
        public string   SupplierCompany { get; set; } = string.Empty;
        public decimal  GrandTotal    { get; set; }
        public string   LinkedDeptName { get; set; } = string.Empty;
        public string?  LinkedPONumber { get; set; }
        public string?  ClosedBy      { get; set; }
        public string?  ClosedByName  { get; set; }
        public DateTime? ClosedDate   { get; set; }
        public string?  CloseRemark   { get; set; }
        public DateTime CreatedAt     { get; set; }
    }

    /// <summary>
    /// ViewModel สำหรับหน้าแสดงรายการ Authorized PRs
    /// </summary>
    public class CreateFromPRViewModel
    {
        public List<PRForPOModel>  PRList       { get; set; } = new();
        public List<ClosedPRModel> ClosedPRList { get; set; } = new();
    }
}
