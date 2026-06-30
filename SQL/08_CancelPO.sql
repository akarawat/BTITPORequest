-- ============================================================
-- Migration 08: Soft-Cancel PO
-- เพิ่ม SP สำหรับยกเลิก PO (Status = -9)
-- รองรับ: Draft(0), Requested(1), Issued(2)
-- Run on: BTITReq database
-- ============================================================
USE [BTITReq]
GO

CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_CancelPO]
    @POId            INT,
    @CancelledBy     NVARCHAR(100),
    @CancelledByName NVARCHAR(200) = NULL,
    @Remark          NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromStatus INT

    -- ตรวจสอบว่ายกเลิกได้ (Draft=0, Requested=1, Issued=2 เท่านั้น)
    SELECT @FromStatus = Status
    FROM ITPO_PurchaseOrders
    WHERE POId = @POId AND Status IN (0, 1, 2)

    IF @FromStatus IS NULL
    BEGIN
        RAISERROR('Cannot cancel: PO not found or status does not allow cancellation.', 16, 1)
        RETURN
    END

    -- อัปเดต Status → -9 (Cancelled)
    UPDATE ITPO_PurchaseOrders
    SET Status    = -9,
        UpdatedAt = GETDATE()
    WHERE POId = @POId

    -- บันทึกประวัติ
    INSERT INTO ITPO_ApprovalHistory
        (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark)
    VALUES
        (@POId, @CancelledBy, @CancelledByName, 'Cancelled', @FromStatus, -9, @Remark)

    SELECT 1 AS Success
END
GO

PRINT 'Created: ITPO_sp_CancelPO'
