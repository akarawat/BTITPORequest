-- ============================================================
-- Script  : 12_ClosedPRAudit.sql
-- Purpose : SP ดึง PR ที่ถูกปิดแล้ว (Status=8, GoodsReceived)
--           พร้อม audit info จาก ITPR_ApprovalHistory
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

CREATE OR ALTER PROCEDURE [dbo].[ITPR_sp_GetClosedPRsForAudit]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pr.PRId,
        pr.PRNumber,
        pr.ReasonToOrder,
        pr.RequesterSam,
        pr.RequesterName,
        pr.SupplierCompany,
        pr.GrandTotal,
        pr.LinkedDeptId,
        pr.LinkedDeptName,
        pr.LinkedPONumber,
        pr.CreatedAt,
        -- ดึงข้อมูลการปิด PR จาก ApprovalHistory (Action='GoodsReceived')
        h.ActionBy      AS ClosedBy,
        h.ActionByName  AS ClosedByName,
        h.ActionDate    AS ClosedDate,
        h.Remark        AS CloseRemark
    FROM ITPR_PurchaseRequisitions pr
    LEFT JOIN (
        -- เอาเฉพาะ record ล่าสุดของ GoodsReceived ต่อ PR
        SELECT PRId, ActionBy, ActionByName, ActionDate, Remark,
               ROW_NUMBER() OVER (PARTITION BY PRId ORDER BY ActionDate DESC) AS rn
        FROM ITPR_ApprovalHistory
        WHERE Action = 'GoodsReceived'
    ) h ON h.PRId = pr.PRId AND h.rn = 1
    WHERE pr.[Status] = 8
    ORDER BY h.ActionDate DESC
END
GO

PRINT '=== 12_ClosedPRAudit.sql done ==='
GO
