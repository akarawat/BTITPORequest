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

        public async Task<string> GeneratePONumberAsync(string deptPrefix = "IT")
        {
            using var conn = _db.GetBTITReqConnection();
            var result = await conn.ExecuteScalarAsync<string>(
                "ITPO_sp_GeneratePONumber",
                new { DeptPrefix = deptPrefix },
                commandType: CommandType.StoredProcedure);
            return result ?? $"{deptPrefix}-{DateTime.Now:yy}-{DateTime.Now:HHmmss}";
        }

        public async Task<int> CreatePOAsync(PORequestModel po, List<POLineItemModel> lineItems, string creatorSam,
            string preAssignedIssuerSam = "", string preAssignedApprover1Sam = "", string preAssignedApprover2Sam = "",
            string requesterDeptCode = "", string deptPrefix = "IT")
        {
            using var conn = _db.GetBTITReqConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                po.PONumber = await conn.ExecuteScalarAsync<string>(
                    "ITPO_sp_GeneratePONumber",
                    new { DeptPrefix = deptPrefix },
                    transaction: tx, commandType: CommandType.StoredProcedure) ?? "";

                var poId = await conn.ExecuteScalarAsync<int>(
                    "ITPO_sp_CreatePO",
                    new
                    {
                        po.PONumber, po.PODate, po.InternalRefAC, po.CreditNo,
                        po.VendorAttn, po.VendorCompany, po.VendorAddress,
                        po.VendorTel, po.VendorFax, po.VendorEmail,
                        po.RefNo, po.Subject, po.Notes,
                        po.Total, po.VatPercent, po.VatAmount,
                        po.GrandTotal, po.GrandTotalText,
                        RequesterSam     = creatorSam,
                        RequesterDeptCode = string.IsNullOrEmpty(requesterDeptCode) ? (string?)null : requesterDeptCode,
                        PreAssignedIssuerSam    = preAssignedIssuerSam,
                        PreAssignedApprover1Sam = preAssignedApprover1Sam,
                        PreAssignedApprover2Sam = preAssignedApprover2Sam,
                        Status = (int)POStatus.Draft,
                        po.OldPONumber, po.InternalContact,
                        po.WorkOrderNo, po.NCRNo, po.IRCRNo, po.ChangeNo
                    },
                    transaction: tx, commandType: CommandType.StoredProcedure);

                foreach (var (item, idx) in lineItems.Select((x, i) => (x, i + 1)))
                {
                    await conn.ExecuteAsync("ITPO_sp_UpsertLineItem",
                        new
                        {
                            POId = poId, LineNo = idx, item.Description,
                            item.Quantity, item.UnitPrice,
                            Amount = Math.Round(item.Quantity * item.UnitPrice, 2)
                        },
                        transaction: tx, commandType: CommandType.StoredProcedure);
                }

                tx.Commit();
                return poId;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<List<UserRoleModel>> GetUsersByRoleAsync(string roleName)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                var result = await conn.QueryAsync<UserRoleModel>(
                    "SELECT * FROM ITPO_UserRoles WHERE RoleName = @RoleName ORDER BY FullName",
                    new { RoleName = roleName });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUsersByRoleAsync failed for role={role}", roleName);
                return new List<UserRoleModel>();
            }
        }

        public async Task UpdatePOAsync(PORequestModel po, List<POLineItemModel> lineItems)
        {
            using var conn = _db.GetBTITReqConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("ITPO_sp_UpdatePO",
                    new
                    {
                        po.POId, po.PODate, po.InternalRefAC, po.CreditNo,
                        po.VendorAttn, po.VendorCompany, po.VendorAddress,
                        po.VendorTel, po.VendorFax, po.VendorEmail,
                        po.RefNo, po.Subject, po.Notes,
                        po.Total, po.VatPercent, po.VatAmount,
                        po.GrandTotal, po.GrandTotalText,
                        po.OldPONumber, po.InternalContact,
                        po.WorkOrderNo, po.NCRNo, po.IRCRNo, po.ChangeNo
                    },
                    transaction: tx, commandType: CommandType.StoredProcedure);

                await conn.ExecuteAsync(
                    "DELETE FROM ITPO_POLineItems WHERE POId = @POId",
                    new { po.POId }, transaction: tx);

                foreach (var (item, idx) in lineItems.Select((x, i) => (x, i + 1)))
                {
                    await conn.ExecuteAsync("ITPO_sp_UpsertLineItem",
                        new
                        {
                            po.POId, LineNo = idx, item.Description,
                            item.Quantity, item.UnitPrice,
                            Amount = Math.Round(item.Quantity * item.UnitPrice, 2)
                        },
                        transaction: tx, commandType: CommandType.StoredProcedure);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<PORequestModel?> GetPOByIdAsync(int poId)
        {
            using var conn = _db.GetBTITReqConnection();
            using var multi = await conn.QueryMultipleAsync(
                "ITPO_sp_GetPOById", new { POId = poId },
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
                "SELECT POId FROM ITPO_PurchaseOrders WHERE PONumber = @PONumber",
                new { PONumber = poNumber });
            return poId.HasValue ? await GetPOByIdAsync(poId.Value) : null;
        }

        public async Task<List<PORequestModel>> GetPOListAsync(
            string? userSam, bool isAdmin, DateTime? dateFrom, DateTime? dateTo, string? status)
        {
            using var conn = _db.GetBTITReqConnection();
            var result = await conn.QueryAsync<PORequestModel>(
                "ITPO_sp_GetPOList",
                new { UserSam = isAdmin ? null : userSam, IsAdmin = isAdmin, DateFrom = dateFrom, DateTo = dateTo, Status = status },
                commandType: CommandType.StoredProcedure);
            return result.ToList();
        }

        public async Task<bool> SubmitPOAsync(int poId, string userSam,
            string requesterName, string requesterTitle,
            string signatureBase64, string signatureImageBase64)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync("ITPO_sp_SubmitPO",
                new
                {
                    POId = poId, UserSam = userSam,
                    RequesterName = requesterName,
                    RequesterTitle = requesterTitle,
                    SignatureBase64 = signatureBase64,
                    SignatureImageBase64 = signatureImageBase64,
                    ToStatus = (int)POStatus.Requested
                },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        public async Task<bool> IssuePOAsync(int poId, string issuerSam, string issuerName, string issuerTitle,
            string signatureBase64, string signatureImageBase64)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync("ITPO_sp_IssuePO",
                new { POId = poId, IssuerSam = issuerSam, IssuerName = issuerName, IssuerTitle = issuerTitle, SignatureBase64 = signatureBase64, SignatureImageBase64 = signatureImageBase64 },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        public async Task<bool> ApprovePOAsync(int poId, int level,
            string approverSam, string approverName, string approverTitle,
            string signatureBase64, string signatureImageBase64, string? remark)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync("ITPO_sp_ApprovePO",
                new { POId = poId, Level = level, ApproverSam = approverSam, ApproverName = approverName, ApproverTitle = approverTitle, SignatureBase64 = signatureBase64, SignatureImageBase64 = signatureImageBase64, Remark = remark },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        public async Task<bool> RejectPOAsync(int poId, int level, string approverSam, string approverName, string remark)
        {
            using var conn = _db.GetBTITReqConnection();
            var rows = await conn.ExecuteAsync("ITPO_sp_RejectPO",
                new { POId = poId, Level = level, ApproverSam = approverSam, ApproverName = approverName, Remark = remark },
                commandType: CommandType.StoredProcedure);
            return rows > 0;
        }

        public async Task<bool> CancelPOAsync(int poId, string cancelledBy, string cancelledByName, string? remark = null)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_CancelPO",
                    new { POId = poId, CancelledBy = cancelledBy, CancelledByName = cancelledByName, Remark = remark },
                    commandType: CommandType.StoredProcedure);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelPOAsync failed poId={poId}", poId);
                return false;
            }
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(
            string userSam, bool isAdmin, DateTime dateFrom, DateTime dateTo,
            string? deptCode = null)
        {
            using var conn = _db.GetBTITReqConnection();
            using var multi = await conn.QueryMultipleAsync(
                "ITPO_sp_GetDashboard",
                new {
                    UserSam  = userSam,
                    IsAdmin  = isAdmin,
                    DeptCode = isAdmin ? null : deptCode,
                    DateFrom = dateFrom,
                    DateTo   = dateTo
                },
                commandType: CommandType.StoredProcedure);

            var summary = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
            var dailyAmounts = (await multi.ReadAsync<DailyAmountData>()).ToList();
            var statusSummary = (await multi.ReadAsync<StatusSummaryData>()).ToList();
            var recentPOs = (await multi.ReadAsync<PORequestModel>()).ToList();
            var pendingActions = (await multi.ReadAsync<PORequestModel>()).ToList();

            return new DashboardViewModel
            {
                DateFrom = dateFrom, DateTo = dateTo,
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

        // ── Email Logs ──────────────────────────────────────────────
        public async Task LogEmailAsync(InsertEmailLogModel model)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync("ITPO_sp_InsertEmailLog",
                    new
                    {
                        model.ToEmail, model.Subject, model.PONumber, model.POId,
                        model.MailType, model.IsSuccess, model.HttpStatus,
                        model.ErrorMsg, model.IsDebug, model.OriginalTo, model.CreatedBy
                    },
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LogEmailAsync failed for {to}", model.ToEmail);
            }
        }

        public async Task<(int totalCount, List<EmailLogModel> logs)> GetEmailLogsAsync(
            DateTime? dateFrom, DateTime? dateTo,
            string? poNumber, string? mailType, bool? isSuccess,
            int pageNum = 1, int pageSize = 50)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                using var multi = await conn.QueryMultipleAsync("ITPO_sp_GetEmailLogs",
                    new { DateFrom = dateFrom, DateTo = dateTo,
                          PONumber = poNumber, MailType = mailType,
                          IsSuccess = isSuccess, PageNum = pageNum, PageSize = pageSize },
                    commandType: CommandType.StoredProcedure);
                var total = await multi.ReadSingleAsync<int>();
                var logs  = (await multi.ReadAsync<EmailLogModel>()).ToList();
                return (total, logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEmailLogsAsync failed");
                return (0, new List<EmailLogModel>());
            }
        }

        // ── PR → PO: ดึง Authorized PRs จาก ITPR_PurchaseRequisitions ────────
        public async Task<List<PRForPOModel>> GetAuthorizedPRsForPOAsync()
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                var result = await conn.QueryAsync<PRForPOModel>(
                    "ITPR_sp_GetAuthorizedPRsForPO",
                    commandType: CommandType.StoredProcedure);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAuthorizedPRsForPOAsync failed");
                return new List<PRForPOModel>();
            }
        }

        public async Task<(PRForPOModel? PR, List<PRLineItemForPOModel> LineItems)> GetPRDetailForPOAsync(int prId)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();

                var prList = await conn.QueryAsync<PRForPOModel>(
                    "ITPR_sp_GetAuthorizedPRsForPO",
                    commandType: CommandType.StoredProcedure);
                var pr = prList.FirstOrDefault(p => p.PRId == prId);

                if (pr == null) return (null, new List<PRLineItemForPOModel>());

                var items = await conn.QueryAsync<PRLineItemForPOModel>(
                    "ITPR_sp_GetPRLineItemsForPO",
                    new { PRId = prId },
                    commandType: CommandType.StoredProcedure);

                return (pr, items.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPRDetailForPOAsync failed prId={prId}", prId);
                return (null, new List<PRLineItemForPOModel>());
            }
        }

        public async Task<(List<PRForPOModel> PRs, List<PRLineItemForPOModel> LineItems)> GetMultiplePRsDetailForPOAsync(IEnumerable<int> prIds)
        {
            var idList = prIds.ToList();
            if (!idList.Any()) return (new(), new());

            try
            {
                using var conn = _db.GetBTITReqConnection();

                // ดึง PR headers ทั้งหมดแล้ว filter
                var allPRs = await conn.QueryAsync<PRForPOModel>(
                    "ITPR_sp_GetAuthorizedPRsForPO",
                    commandType: CommandType.StoredProcedure);
                var matchedPRs = allPRs.Where(p => idList.Contains(p.PRId)).ToList();

                // ดึง Line Items จากทุก PR พร้อมกัน
                var allItems = new List<PRLineItemForPOModel>();
                foreach (var id in idList)
                {
                    var items = await conn.QueryAsync<PRLineItemForPOModel>(
                        "ITPR_sp_GetPRLineItemsForPO",
                        new { PRId = id },
                        commandType: CommandType.StoredProcedure);
                    allItems.AddRange(items);
                }

                return (matchedPRs, allItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMultiplePRsDetailForPOAsync failed prIds={ids}", string.Join(",", idList));
                return (new(), new());
            }
        }

        public async Task<(bool Success, string PRNumber, string RequesterEmail, string RequesterName)>
            ClosePRAsync(int prId, string closedBy, string closedByName,
                         string? poNumber = null, string? remark = null)
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                // SP returns 2 result sets: row 0 = PR info+email, row 1 = Success flag
                using var multi = await conn.QueryMultipleAsync(
                    "ITPR_sp_ClosePR",
                    new { PRId = prId, ClosedBy = closedBy, ClosedByName = closedByName,
                          PONumber = poNumber, Remark = remark },
                    commandType: CommandType.StoredProcedure);

                var info = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
                await multi.ReadAsync(); // consume Success result set

                if (info == null) return (false, "", "", "");
                return (true,
                    (string)(info.PRNumber ?? ""),
                    (string)(info.RequesterEmail ?? ""),
                    (string)(info.RequesterName ?? ""));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClosePRAsync failed prId={prId}", prId);
                return (false, "", "", "");
            }
        }

        public async Task<List<ClosedPRModel>> GetClosedPRsAsync()
        {
            try
            {
                using var conn = _db.GetBTITReqConnection();
                var result = await conn.QueryAsync<ClosedPRModel>(
                    "ITPR_sp_GetClosedPRsForAudit",
                    commandType: CommandType.StoredProcedure);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetClosedPRsAsync failed");
                return new List<ClosedPRModel>();
            }
        }

        public async Task LinkPRsToPOAsync(IEnumerable<int> prIds, string poNumber)
        {
            var idList = prIds?.ToList();
            if (idList == null || idList.Count == 0 || string.IsNullOrEmpty(poNumber)) return;

            try
            {
                using var conn = _db.GetBTITReqConnection();
                await conn.ExecuteAsync(
                    "ITPR_sp_LinkPRsToPO",
                    new { PRIds = string.Join(",", idList), PONumber = poNumber },
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LinkPRsToPOAsync failed poNumber={poNumber}", poNumber);
            }
        }
    }
}
