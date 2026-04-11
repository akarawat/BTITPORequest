namespace BTITPORequest.Models
{
    // ── Request to BTDigitalSign /api/auth/token ──────────────────
    public class DsTokenRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // ── Request to BTDigitalSign /api/sign ────────────────────────
    public class DsSignRequest
    {
        public string DataToSign { get; set; } = string.Empty;    // e.g. "PO BTPO251234"
        public string ReferenceId { get; set; } = string.Empty;   // PONumber
        public string Purpose { get; set; } = string.Empty;       // "Requested" | "Issued" | "Authorized" | "Completed"
        public string? Department { get; set; }
        public string? Remarks { get; set; }
    }

    // ── Request to BTDigitalSign /api/pdf/sign ────────────────────
    public class DsPdfSignRequest
    {
        public string PdfBase64 { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string Reason { get; set; } = "Approved";
        public string Location { get; set; } = "Bangkok, Thailand";
        public string? SignerUsername { get; set; }
        public string? SignerFullName { get; set; }
        public string? SignerRole { get; set; }
        public string? WebSource { get; set; }
        public string? DocumentType { get; set; }
        public int SignaturePage { get; set; } = 1;
        public float SignatureX { get; set; } = 36f;
        public float SignatureY { get; set; } = 36f;
        public float SignatureWidth { get; set; } = 200f;
        public float SignatureHeight { get; set; } = 60f;
    }

    // ── Response from /api/auth/token ─────────────────────────────
    public class DsTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    // ── Response from /api/sign ───────────────────────────────────
    public class DsSignResult
    {
        public bool IsSuccess { get; set; }
        public string SignatureBase64 { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedAt { get; set; }
        public string CertThumbprint { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    // ── Response from /api/pdf/sign ───────────────────────────────
    public class DsPdfSignResult
    {
        public bool IsSuccess { get; set; }
        public string PdfBase64 { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // ── Generic wrapper: ApiResponse<T> ──────────────────────────
    public class DsApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }
}
