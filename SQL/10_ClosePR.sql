-- ============================================================
-- Script  : 10_ClosePR.sql
-- Purpose : ปิด PR (GoodsReceived = 8) โดย Admin ของ PO system
--           บันทึก ITPR_ApprovalHistory + return requester email
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

CREATE OR ALTER PROCEDURE [dbo].[ITPR_sp_ClosePR]
    @PRId         INT,
    @ClosedBy     NVARCHAR(100),           -- SamAcc ของ Admin
    @ClosedByName NVARCHAR(200) = NULL,
    @PONumber     NVARCHAR(50)  = NULL,    -- PO ที่เชื่อมอยู่ (optional)
    @Remark       NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. ตรวจสอบ PR ว่า Status=6 (Authorized) ──────────────
    DECLARE @FromStatus INT
    SELECT @FromStatus = [Status]
    FROM ITPR_PurchaseRequisitions
    WHERE PRId = @PRId AND [Status] = 6

    IF @FromStatus IS NULL
    BEGIN
        RAISERROR('PR not found or not in Authorized status.', 16, 1)
        RETURN
    END

    -- ── 2. เปลี่ยน Status → 8 (GoodsReceived) ────────────────
    UPDATE ITPR_PurchaseRequisitions
    SET [Status]   = 8,
        UpdatedAt  = GETDATE()
    WHERE PRId = @PRId

    -- ── 3. บันทึก ITPR_ApprovalHistory ───────────────────────
    DECLARE @FinalRemark NVARCHAR(500)
    SET @FinalRemark = ISNULL(@Remark, '')
    IF @PONumber IS NOT NULL AND @PONumber <> ''
        SET @FinalRemark = CONCAT('Linked PO: ', @PONumber,
                                  CASE WHEN @FinalRemark <> '' THEN ' — ' + @FinalRemark ELSE '' END)

    INSERT INTO ITPR_ApprovalHistory
        (PRId, ActionBy, ActionByName, Action, Remark, FromStatus, ToStatus)
    VALUES
        (@PRId, @ClosedBy, @ClosedByName, 'GoodsReceived', @FinalRemark, 6, 8)

    -- ── 4. Return requester info + email (JOIN BT_HR same server) ──
    SELECT
        pr.PRId,
        pr.PRNumber,
        pr.RequesterSam,
        pr.RequesterName,
        ISNULL(hr.UEMAIL, '') AS RequesterEmail
    FROM ITPR_PurchaseRequisitions pr
    LEFT JOIN [BT_HR].[dbo].[onl_TBADUsers] hr
           ON hr.emp_code COLLATE Thai_CI_AS = pr.RequesterSam   -- match by SamAcc/emp_code
    WHERE pr.PRId = @PRId

    SELECT 1 AS Success
END
GO

PRINT '=== 10_ClosePR.sql done ==='
GO
