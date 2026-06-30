-- ============================================================
-- Migration 07: Stored Procedures สำหรับ "New PO from PR"
-- ดึง PR ที่ Authorized แล้ว (Status=6) มาทำ PO
-- Run on: BTITReq database
-- ============================================================
USE [BTITReq]
GO

-- ── 1. ดึง PR ทั้งหมดที่ Status = 6 (Authorized/Completed) ──────
CREATE OR ALTER PROCEDURE [dbo].[ITPR_sp_GetAuthorizedPRsForPO]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        PRId,
        PRNumber,
        ReasonToOrder,
        RequesterSam,
        RequesterName,
        SupplierCompany,
        SupplierVC,
        SupplierContact,
        SupplierEmail,
        SupplierTel,
        SupplierFax,
        SupplierQuoteRef,
        GrandTotal,
        LinkedDeptId,
        LinkedDeptName,
        CreatedAt,
        UpdatedAt
    FROM ITPR_PurchaseRequisitions
    WHERE Status = 6
    ORDER BY UpdatedAt DESC
END
GO

-- ── 2. ดึง Line Items ของ PR สำหรับ pre-fill PO form ────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPR_sp_GetPRLineItemsForPO]
    @PRId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ItemId,
        PRId,
        LineNo,
        BTNumber,
        Description,
        BrandModel,
        AccountCode,
        CostDept,
        Quantity,
        Unit,
        UnitPrice,
        ROUND(Quantity * UnitPrice, 2) AS Amount
    FROM ITPR_PRLineItems
    WHERE PRId = @PRId
    ORDER BY LineNo
END
GO

PRINT 'Created: ITPR_sp_GetAuthorizedPRsForPO, ITPR_sp_GetPRLineItemsForPO'
