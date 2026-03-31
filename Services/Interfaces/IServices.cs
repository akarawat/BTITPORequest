using BTITPORequest.Models;

namespace BTITPORequest.Services.Interfaces
{
    // ==============================
    // Auth Service
    // ==============================
    public interface IAuthService
    {
        Task<(bool Success, UserSessionModel? User, string ErrorMessage)> LoginAsync(string username, string password);
        Task<bool> ValidateTokenAsync(string token);
        Task<HRUserModel?> GetHRUserAsync(string samAcc);
        Task<List<HRUserModel>> GetAllUsersAsync();
    }

    // ==============================
    // PO Service
    // ==============================
    public interface IPOService
    {
        // CRUD
        Task<int> CreatePOAsync(PORequestModel po, List<POLineItemModel> lineItems, string creatorSam);
        Task UpdatePOAsync(PORequestModel po, List<POLineItemModel> lineItems);
        Task<PORequestModel?> GetPOByIdAsync(int poId);
        Task<PORequestModel?> GetPOByNumberAsync(string poNumber);
        Task<List<PORequestModel>> GetPOListAsync(string? userSam, bool isAdmin, DateTime? dateFrom, DateTime? dateTo, string? status);

        // Workflow
        Task<bool> SubmitPOAsync(int poId, string userSam, string signUrl);
        Task<bool> IssuePOAsync(int poId, string issuerSam, string issuerName, string issuerTitle, string signUrl);
        Task<bool> ApprovePOAsync(int poId, int level, string approverSam, string approverName, string approverTitle, string signUrl, string? remark);
        Task<bool> RejectPOAsync(int poId, int level, string approverSam, string approverName, string remark);

        // Dashboard
        Task<DashboardViewModel> GetDashboardDataAsync(string userSam, bool isAdmin, DateTime dateFrom, DateTime dateTo);

        // Generate PO Number
        Task<string> GeneratePONumberAsync();
    }

    // ==============================
    // Digital Sign Service
    // ==============================
    public interface IDigitalSignService
    {
        Task<(bool Success, string? Token, string? UserInfo)> AuthenticateAsync(string username, string password);
        Task<bool> HealthCheckAsync();
        Task<(bool Success, string? SignatureUrl, string? Error)> RequestSignatureAsync(string token, int poId, string documentType);
        Task<(bool Success, string? SignatureUrl, string? Error)> ApproveSignatureAsync(string token, int poId, string documentType);
    }

    // ==============================
    // PDF Service
    // ==============================
    public interface IPdfService
    {
        Task<byte[]> GeneratePOPdfAsync(PORequestModel po);
    }
}
