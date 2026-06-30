-- ============================================================
-- Script  : 11_LinkedPONumber.sql
-- Purpose : เพิ่ม LinkedPONumber ใน ITPR_PurchaseRequisitions
--           เพื่อ track ว่า PR นี้ถูกเอาไปเปิด PO แล้ว
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

-- ── 1. เพิ่มคอลัมน์ LinkedPONumber ────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ITPR_PurchaseRequisitions')
      AND name = 'LinkedPONumber'
)
BEGIN
    ALTER TABLE [dbo].[ITPR_PurchaseRequisitions]
        ADD [LinkedPONumber] NVARCHAR(30) NULL
    PRINT 'Added column: LinkedPONumber'
END
ELSE
    PRINT 'Column LinkedPONumber already exists'
GO

-- ── 2. SP: Link PRs → PO Number (เรียกหลัง Create PO สำเร็จ) ──
CREATE OR ALTER PROCEDURE [dbo].[ITPR_sp_LinkPRsToPO]
    @PRIds   NVARCHAR(MAX),   -- comma-separated PRIds เช่น '1,2,5'
    @PONumber NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[ITPR_PurchaseRequisitions]
    SET    LinkedPONumber = @PONumber,
           UpdatedAt      = GETDATE()
    WHERE  PRId IN (
        SELECT TRY_CAST(value AS INT)
        FROM   STRING_SPLIT(@PRIds, ',')
        WHERE  TRY_CAST(value AS INT) IS NOT NULL
    )
    AND [Status] = 6   -- เฉพาะ Authorized เท่านั้น
END
GO

-- ── 3. อัปเดต ITPR_sp_GetAuthorizedPRsForPO คืน LinkedPONumber ─
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
        LinkedPONumber,
        CreatedAt,
        UpdatedAt
    FROM ITPR_PurchaseRequisitions
    WHERE Status = 6
    ORDER BY
        CASE WHEN LinkedPONumber IS NULL THEN 0 ELSE 1 END,  -- ยังไม่ link ขึ้นก่อน
        UpdatedAt DESC
END
GO

PRINT '=== 11_LinkedPONumber.sql done ==='
GO
