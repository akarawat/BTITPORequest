using BTITPORequest.Data;
using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using Dapper;
using System.Data;

namespace BTITPORequest.Services
{
    public class POService : IPOService
    {
        private readonly DbContext _db;
        private readonly ILogger<POService> _logger;

        public POService(DbContext db, ILogger<POService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ──────────────────────────────
        //  Generate PO Number
        // ──────────────────────────────
        public async Task<string> GeneratePONumberAsync()
        {
            using var conn = _db.GetBTITReqConnection();
            var result = await conn.ExecuteScalarAsync<string>(
                "EXEC ITPO_sp_GeneratePONumber");
            return result ?? $"PO{DateTime.Now:yyyyMMddHHmm}";
        }

        // ──────────────────────────────
        //  Create PO
        // ──────────────────────────────
        public async Task<int> CreatePOAsync(PORequestModel po, List<POLineItemModel> lineItems, string creatorSam)
        {
            using var conn = _db.GetBTITReqConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                po.PONumber = await conn.ExecuteScalarAsync<string>(
                    "EXEC ITPO_sp_GeneratePONumber", transaction: tx) ?? "";

                var poId = await conn.ExecuteScalarAsync<int>(
                    "ITPO_sp_CreatePO",
                    new
                    {
                        po.PONumber, po.PODate, po.InternalRefAC, po.CreditNo,
                        po.VendorAttn, po.VendorCompany, po.VendorAddress,
                        po.VendorTel, po.VendorFax, po.VendorEmail,
                        po.RefNo, po.Subject, po.Notes,
                        po.Total, po.VatPercent, po.VatAmount, po.GrandTotal, po.GrandTotalText,
                        RequesterSam = creatorSam,
                        Status = (int)POStatus.Draft
                    },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure);

                foreach (var (item, idx) in lineItems.Select((x, i) => (x, i + 1)))
                {
                    await conn.ExecuteAsync(
                        "ITPO_sp_UpsertLineItem",
                        new
                        {
                            POId = poId, LineNo = idx,
                            item.Description, item.Quantity, item.UnitPrice,
                            Amount = item.Quantity * item.UnitPrice
                        },
                        transaction: tx,
                        commandType: CommandType.StoredProcedure);
                }

                tx.Commit();
                return poId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ──────────────────────────────
        //  Update PO (Draft only)
        // ──────────────────────────────
        public async Task UpdatePOAsync(PORequestModel po, List<POLineItemModel> lineItems)
        {
            using var conn = _db.GetBTITReqConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(
                    "ITPO_sp_UpdatePO",
                    new
                    {
                        po.POId, po.PODate, po.InternalRefAC, po.CreditNo,
                        po.VendorAttn, po.VendorCompany, po.VendorAddress,
                        po.VendorTel, po.VendorFax, po.VendorEmail,
                        po.RefNo, po.Subject, po.Notes,
                        po.Total, po.VatPercent, po.VatAmount, po.GrandTotal, po.GrandTotalText
                    },
                    transaction: tx,
                    commandType: CommandType.StoredProcedure);

                // Delete existing items
                await conn.ExecuteAsync(
                    "DELETE FROM ITPO_POLineItems WHERE POId = @POId",
                    new { po.POId }, transaction: tx);

                foreach (var (item, idx) in lineItems.Select((x, i) => (x, i + 1)))
                {
                    await conn.ExecuteAsync(
                        "ITPO_sp_UpsertLineItem",
                        new
                        {
                            po.POId, LineNo = idx,
                            item.Description, item.Quantity, item.UnitPrice,
                            Amount = item.Quantity * item.UnitPrice
                        },
                        transaction: tx,
                        commandType: CommandType.StoredProcedure);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ──────────────────────────────
        //  Get Single PO
        // ──────────────────────────────
        public async Task<PORequestModel?> GetPOByIdAsync(int poId)
        {
            using var conn = _db.GetBTITReqConnection();
            using var multi = await conn.QueryMultipleAsync(
                "ITPO_sp_GetPOById",
                new { POId = poId },
                commandType: CommandType.StoredProcedure);

            var po = (await multi.ReadAsync<PORequestModel>()).FirstOrDefault();
            if (po == null) return null;

            po.LineItems = (await multi.ReadAsync<POLineItemModel>()).ToList();
            po.ApprovalHistory = (await multi.ReadAsync<POApprovalHistoryModel>()).ToList();
            return po;
        }

        public async Task<PORequestModel?> GetPOByNumberAsync(string poNumber)
        {
            using var conn = _db.GetBTITReqConnection();
            var poId = await conn.ExecuteScalarAsync<int?>(
                "SELECT POId FROM ITPO_PurchaseOrders WHERE PONumber = @PONumber", new { PONumber = poNumber });
            return poId.HasValue ? await GetPOByIdAsync(poId.Value) : null;
        }

        // ──────────────────────────────
        //  Get PO List
        // ──────────────────────────────
        public async Task<List<PORequestModel>> GetPOListAsync(
            string? userSam, bool isAdmin, DateTime? dateFrom, DateTime? dateTo, string? status)
        {
            using var conn = _db.GetBTITReqConnection();
            var result = await conn.QueryAsync<PORequestModel>(
                "ITPO_sp_GetPOList",
                new
                {
                    UserSam = isAdmin ? null : userSam,
                    IsAdmin = isAdmin,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    Status = status
                },
                commandType: CommandType.StoredProcedure);
            return result.ToList();
        }

        // ──────────────────────────────
        //  Submit (Requested)
        // ──────────────────────────────
        public async Task<bool> SubmitPOAsync(int poId, string userSam, string signUrl)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync(
                "ITPO_sp_SubmitPO",
                new { POId = poId, UserSam = userSam, SignUrl = signUrl, ToStatus = (int)POStatus.Requested },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        // ──────────────────────────────
        //  Issue (Issued)
        // ──────────────────────────────
        public async Task<bool> IssuePOAsync(int poId, string issuerSam, string issuerName, string issuerTitle, string signUrl)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync(
                "ITPO_sp_IssuePO",
                new { POId = poId, IssuerSam = issuerSam, IssuerName = issuerName, IssuerTitle = issuerTitle, SignUrl = signUrl },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        // ──────────────────────────────
        //  Approve / Reject
        // ──────────────────────────────
        public async Task<bool> ApprovePOAsync(
            int poId, int level, string approverSam, string approverName, string approverTitle, string signUrl, string? remark)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync(
                "ITPO_sp_ApprovePO",
                new { POId = poId, Level = level, ApproverSam = approverSam, ApproverName = approverName, ApproverTitle = approverTitle, SignUrl = signUrl, Remark = remark },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        public async Task<bool> RejectPOAsync(int poId, int level, string approverSam, string approverName, string remark)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync(
                "ITPO_sp_RejectPO",
                new { POId = poId, Level = level, ApproverSam = approverSam, ApproverName = approverName, Remark = remark },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        // ──────────────────────────────
        //  Dashboard
        // ──────────────────────────────
        public async Task<DashboardViewModel> GetDashboardDataAsync(
            string userSam, bool isAdmin, DateTime dateFrom, DateTime dateTo)
        {
            using var conn = _db.GetBTITReqConnection();
            using var multi = await conn.QueryMultipleAsync(
                "ITPO_sp_GetDashboard",
                new { UserSam = isAdmin ? null : userSam, IsAdmin = isAdmin, DateFrom = dateFrom, DateTo = dateTo },
                commandType: CommandType.StoredProcedure);

            var summary = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
            var dailyAmounts = (await multi.ReadAsync<DailyAmountData>()).ToList();
            var statusSummary = (await multi.ReadAsync<StatusSummaryData>()).ToList();
            var recentPOs = (await multi.ReadAsync<PORequestModel>()).ToList();
            var pendingActions = (await multi.ReadAsync<PORequestModel>()).ToList();

            return new DashboardViewModel
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                TotalPO = (int)(summary?.TotalPO ?? 0),
                TotalAmount = (decimal)(summary?.TotalAmount ?? 0),
                PendingCount = (int)(summary?.PendingCount ?? 0),
                CompletedCount = (int)(summary?.CompletedCount ?? 0),
                RejectedCount = (int)(summary?.RejectedCount ?? 0),
                DraftCount = (int)(summary?.DraftCount ?? 0),
                DailyAmounts = dailyAmounts,
                StatusSummary = statusSummary,
                RecentPOs = recentPOs,
                PendingMyAction = pendingActions
            };
        }
    }
}
