-- ============================================================
-- Script  : 16_AdminCancelAnyStatus.sql
-- Purpose : ขยาย ITPO_sp_CancelPO ให้ Admin ยกเลิกได้ทุก Status
--           (เดิมจำกัดแค่ Draft/Requested/Issued)
--           Status ที่ยกเลิกไม่ได้ : -9 (Cancelled), -1/-2 (Rejected)
-- Run on  : BTITReq database
-- ============================================================

CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_CancelPO]
    @POId            INT,
    @CancelledBy     NVARCHAR(100),
    @CancelledByName NVARCHAR(200) = NULL,
    @Remark          NVARCHAR(500) = NULL,
    @IsAdmin         BIT           = 0       -- NEW: Admin ยกเลิกได้ทุก status
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromStatus INT;

    -- ตรวจสอบ status ที่อนุญาต
    --   Non-admin : Draft(0), Requested(1), Issued(2) เท่านั้น
    --   Admin     : ทุก status ยกเว้น Cancelled(-9) และ Rejected(-1,-2)
    IF @IsAdmin = 1
    BEGIN
        SELECT @FromStatus = Status
        FROM ITPO_PurchaseOrders
        WHERE POId = @POId
          AND Status NOT IN (-9, -1, -2);
    END
    ELSE
    BEGIN
        SELECT @FromStatus = Status
        FROM ITPO_PurchaseOrders
        WHERE POId = @POId
          AND Status IN (0, 1, 2);
    END

    IF @FromStatus IS NULL
    BEGIN
        RAISERROR('Cannot cancel: PO not found or status does not allow cancellation.', 16, 1);
        RETURN;
    END

    -- อัปเดต Status → -9 (Cancelled)
    UPDATE ITPO_PurchaseOrders
    SET Status    = -9,
        UpdatedAt = GETDATE()
    WHERE POId = @POId;

    -- บันทึกประวัติ
    INSERT INTO ITPO_ApprovalHistory
        (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark)
    VALUES
        (@POId, @CancelledBy, @CancelledByName, 'Cancelled', @FromStatus, -9, @Remark);

    SELECT 1 AS Success;
END
GO

PRINT '=== 16_AdminCancelAnyStatus.sql done ==='
GO
