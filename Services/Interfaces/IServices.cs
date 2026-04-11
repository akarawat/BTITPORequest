using BTITPORequest.Models;

namespace BTITPORequest.Services.Interfaces
{
    // ──────────────────────────────────────────────────────────
    // Auth Service
    // ──────────────────────────────────────────────────────────
    public interface IAuthService
    {
        Task<UserSessionModel?> BuildSessionFromSsoAsync(
            string ssoId, string adUser, string email, string fname, string depart);
        Task<HRUserModel?> GetHRUserAsync(string samAcc);
        Task<List<HRUserModel>> GetAllUsersAsync();
    }

    // ──────────────────────────────────────────────────────────
    // PO Service
    // ──────────────────────────────────────────────────────────
    public interface IPOService
    {
        Task<int> CreatePOAsync(PORequestModel po, List<POLineItemModel> lineItems, string creatorSam);
        Task UpdatePOAsync(PORequestModel po, List<POLineItemModel> lineItems);
        Task<PORequestModel?> GetPOByIdAsync(int poId);
        Task<PORequestModel?> GetPOByNumberAsync(string poNumber);
        Task<List<PORequestModel>> GetPOListAsync(
            string? userSam, bool isAdmin, DateTime? dateFrom, DateTime? dateTo, string? status);
        Task<bool> SubmitPOAsync(int poId, string userSam,
            string signatureBase64, string signatureImageBase64);
        Task<bool> IssuePOAsync(int poId, string issuerSam, string issuerName, string issuerTitle,
            string signatureBase64, string signatureImageBase64);
        Task<bool> ApprovePOAsync(int poId, int level,
            string approverSam, string approverName, string approverTitle,
            string signatureBase64, string signatureImageBase64, string? remark);
        Task<bool> RejectPOAsync(int poId, int level,
            string approverSam, string approverName, string remark);
        Task<DashboardViewModel> GetDashboardDataAsync(
            string userSam, bool isAdmin, DateTime dateFrom, DateTime dateTo);
        Task<string> GeneratePONumberAsync();
    }

    // ──────────────────────────────────────────────────────────
    // Digital Sign Service
    // ──────────────────────────────────────────────────────────
    public interface IDigitalSignService
    {
        Task<bool> HealthCheckAsync();

        // ── JWT Token (/api/auth/token) ─────────────────────
        Task<string?> GetTokenAsync();

        // ── Cryptographic Sign (/api/sign) ──────────────────
        Task<DsSignResult?> SignDataAsync(
            string referenceId, string purpose,
            string signerUsername, string signerFullName,
            string? department = null, string? remarks = null);

        // ── PDF Sign (/api/pdf/sign) ─────────────────────────
        Task<byte[]?> SignPdfAsync(
            byte[] pdfBytes, string documentName, string referenceId,
            string signerUsername, string signerFullName, string signerRole,
            int signPage = 1,
            float x = 36f, float y = 36f, float width = 200f, float height = 60f);

        // ── Signature Image (/api/signature-registry/image/{sam}) ──
        /// <summary>
        /// ดึงรูปภาพลายเซ็นต์จาก bt_digitalsign
        /// ใช้ X-Api-Key header (InternalApiKey)
        /// คืนค่าเป็น base64 PNG string
        /// </summary>
        Task<string?> GetSignatureImageAsync(string samAcc);
    }

    // ──────────────────────────────────────────────────────────
    // PDF Service
    // ──────────────────────────────────────────────────────────
    public interface IPdfService
    {
        Task<byte[]> GeneratePOPdfAsync(
            PORequestModel po, string signerUsername, string signerFullName);
    }
}
