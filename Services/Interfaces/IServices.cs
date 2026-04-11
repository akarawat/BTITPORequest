using BTITPORequest.Models;

namespace BTITPORequest.Services.Interfaces
{
    public interface IAuthService
    {
        Task<UserSessionModel?> BuildSessionFromSsoAsync(
            string ssoId, string adUser, string email, string fname, string depart);
        Task<HRUserModel?> GetHRUserAsync(string samAcc);
        Task<List<HRUserModel>> GetAllUsersAsync();
    }

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

    public interface IDigitalSignService
    {
        Task<bool> HealthCheckAsync();

        /// <summary>
        /// ดึงรูปลายเซ็นต์ของ user จาก bt_digitalsign
        /// GET /api/signature-registry/user/{samAcc}
        /// ต้องส่ง X-Api-Key: InternalApiKey
        /// </summary>
        Task<string?> GetSignatureImageAsync(string samAcc);

        /// <summary>ไม่ใช้แล้ว (Windows SSO) — คืน null เสมอ</summary>
        Task<string?> GetTokenAsync();

        /// <summary>
        /// POST /api/sign (Windows SSO)
        /// สร้าง cryptographic signature สำหรับ workflow step
        /// </summary>
        Task<DsSignResult?> SignDataAsync(
            string referenceId, string purpose,
            string signerUsername, string signerFullName,
            string? department = null, string? remarks = null);

        /// <summary>
        /// POST /api/pdf/sign (Windows SSO)
        /// Embed digital signature ลง PDF
        /// </summary>
        Task<byte[]?> SignPdfAsync(
            byte[] pdfBytes, string documentName, string referenceId,
            string signerUsername, string signerFullName,
            string signerRole, int signPage = 1,
            float x = 36f, float y = 36f,
            float width = 200f, float height = 60f);
    }

    public interface IPdfService
    {
        Task<byte[]> GeneratePOPdfAsync(PORequestModel po, string signerUsername, string signerFullName);
    }
}
