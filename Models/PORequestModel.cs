using System.ComponentModel.DataAnnotations;

namespace BTITPORequest.Models
{
    // ==============================
    // PO Status Enum
    // ==============================
    public enum POStatus
    {
        Draft = 0,
        Requested = 1,       // Requester submitted & signed
        Issued = 2,          // Issuer (Network Admin) signed
        PendingApproval1 = 3,// Waiting first approver
        Authorized = 4,      // First approver signed
        PendingApproval2 = 5,// Waiting second approver
        Completed = 6,       // Second approver signed - END
        RejectedByApprover1 = -1, // Rejected back to Requested
        RejectedByApprover2 = -2  // Rejected back to Authorized
    }

    // ==============================
    // Main PO Request
    // ==============================
    public class PORequestModel
    {
        public int POId { get; set; }
        public string PONumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a date")]
        [DataType(DataType.Date)]
        public DateTime PODate { get; set; } = DateTime.Today;

        public string InternalRefAC { get; set; } = string.Empty;   // e.g. "438700/310"
        public string CreditNo { get; set; } = string.Empty;

        // Vendor Info
        [Required(ErrorMessage = "Attn is required")]
        public string VendorAttn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Company name is required")]
        public string VendorCompany { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        public string VendorAddress { get; set; } = string.Empty;
        public string VendorTel { get; set; } = string.Empty;
        public string VendorFax { get; set; } = string.Empty;
        public string VendorEmail { get; set; } = string.Empty;

        public string RefNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Subject is required")]
        public string Subject { get; set; } = string.Empty;

        // Financials
        public decimal Total { get; set; }
        public decimal VatPercent { get; set; } = 0;
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string GrandTotalText { get; set; } = string.Empty;

        // Notes
        public string Notes { get; set; } = "- Please sign & fax back to us after receipt P/O for order confirmation.\r\n- Terms of payment: 30 day net.\r\n- Lead time / delivery: within 3 - 5 Days of issued P/O date.";

        // Status & Workflow
        public POStatus Status { get; set; } = POStatus.Draft;
        public string StatusLabel => Status switch
        {
            POStatus.Draft => "Draft",
            POStatus.Requested => "Requested",
            POStatus.Issued => "Issued",
            POStatus.PendingApproval1 => "Pending Approval",
            POStatus.Authorized => "Authorized",
            POStatus.PendingApproval2 => "Pending Final Approval",
            POStatus.Completed => "Completed",
            POStatus.RejectedByApprover1 => "Rejected",
            POStatus.RejectedByApprover2 => "Rejected",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            POStatus.Draft => "secondary",
            POStatus.Requested => "primary",
            POStatus.Issued => "info",
            POStatus.PendingApproval1 => "warning",
            POStatus.Authorized => "info",
            POStatus.PendingApproval2 => "warning",
            POStatus.Completed => "success",
            POStatus.RejectedByApprover1 => "danger",
            POStatus.RejectedByApprover2 => "danger",
            _ => "secondary"
        };

        // Requester
        public string RequesterSam { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string RequesterTitle { get; set; } = string.Empty;
        public string RequesterSignUrl { get; set; } = string.Empty;
        public DateTime? RequestedDate { get; set; }

        // Issuer
        public string IssuerSam { get; set; } = string.Empty;
        public string IssuerName { get; set; } = string.Empty;
        public string IssuerTitle { get; set; } = string.Empty;
        public string IssuerSignUrl { get; set; } = string.Empty;
        public DateTime? IssuedDate { get; set; }

        // Approver 1 (Authorized)
        public string Approver1Sam { get; set; } = string.Empty;
        public string Approver1Name { get; set; } = string.Empty;
        public string Approver1Title { get; set; } = string.Empty;
        public string Approver1SignUrl { get; set; } = string.Empty;
        public DateTime? Approver1Date { get; set; }
        public string? Approver1Remark { get; set; }

        // Approver 2 (Final)
        public string Approver2Sam { get; set; } = string.Empty;
        public string Approver2Name { get; set; } = string.Empty;
        public string Approver2Title { get; set; } = string.Empty;
        public string Approver2SignUrl { get; set; } = string.Empty;
        public DateTime? Approver2Date { get; set; }
        public string? Approver2Remark { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public List<POLineItemModel> LineItems { get; set; } = new();
        public List<POApprovalHistoryModel> ApprovalHistory { get; set; } = new();

        // Current user context (not persisted)
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanIssue { get; set; }
        public bool CanApprove1 { get; set; }
        public bool CanApprove2 { get; set; }
        public bool CanDownloadPDF { get; set; }
    }

    // ==============================
    // PO Line Item
    // ==============================
    public class POLineItemModel
    {
        public int ItemId { get; set; }
        public int POId { get; set; }
        public int LineNo { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; } = 1;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        public decimal Amount => Math.Round(Quantity * UnitPrice, 2);
    }

    // ==============================
    // Approval History
    // ==============================
    public class POApprovalHistoryModel
    {
        public int HistoryId { get; set; }
        public int POId { get; set; }
        public string ActionBy { get; set; } = string.Empty;
        public string ActionByName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;   // Submitted, Issued, Approved, Rejected
        public string? Remark { get; set; }
        public POStatus FromStatus { get; set; }
        public POStatus ToStatus { get; set; }
        public string SignUrl { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
    }

    // ==============================
    // Create/Edit ViewModel
    // ==============================
    public class POCreateViewModel
    {
        public PORequestModel PO { get; set; } = new();
        public List<POLineItemModel> LineItems { get; set; } = new();
        public string CurrentUserSam { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
    }

    // ==============================
    // List ViewModel
    // ==============================
    public class POListViewModel
    {
        public List<PORequestModel> POList { get; set; } = new();
        public string? FilterStatus { get; set; }
        public string? SearchText { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int TotalCount { get; set; }
        public string CurrentUserSam { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    // ==============================
    // Approve ViewModel
    // ==============================
    public class POApproveViewModel
    {
        public PORequestModel PO { get; set; } = new();
        public string Action { get; set; } = string.Empty;   // approve | reject
        public string? Remark { get; set; }
        public int ApprovalLevel { get; set; }               // 1 | 2
    }
}
