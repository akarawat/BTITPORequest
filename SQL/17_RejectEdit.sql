-- ============================================================
-- Script  : 17_RejectEdit.sql
-- Purpose : Feature "Reject Edit" — Admin ส่ง PO กลับให้แก้ไข
--           โดยไม่ผ่าน Workflow ใหม่ (กลับสถานะเดิมหลัง Re-Submit)
-- Run on  : BTITReq database
-- ============================================================

-- ── 1. เพิ่ม Columns ────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'RevisionNo'
)
BEGIN
    ALTER TABLE [dbo].[ITPO_PurchaseOrders]
    ADD [RevisionNo] INT NOT NULL DEFAULT 0;
    PRINT 'Column RevisionNo added.';
END
ELSE
    PRINT 'Column RevisionNo already exists — skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'PreRevisionStatus'
)
BEGIN
    ALTER TABLE [dbo].[ITPO_PurchaseOrders]
    ADD [PreRevisionStatus] INT NULL;
    PRINT 'Column PreRevisionStatus added.';
END
ELSE
    PRINT 'Column PreRevisionStatus already exists — skipped.';
GO

-- ── 2. SP: Reject Edit PO (Admin Only) ───────────────────────
--   Status → -8 (Under Revision)
--   RevisionNo += 1
--   PreRevisionStatus = ค่าปัจจุบัน (เพื่อกลับไปหลัง Re-Submit)
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_RejectEditPO]
    @POId        INT,
    @AdminSam    NVARCHAR(150),
    @AdminName   NVARCHAR(200) = NULL,
    @Remark      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentStatus INT;

    SELECT @CurrentStatus = Status
    FROM ITPO_PurchaseOrders
    WHERE POId = @POId;

    -- ยกเว้น: ยังไม่ submit (Draft=0), ถูก cancel/reject/ระหว่างแก้แล้ว
    IF @CurrentStatus IS NULL OR @CurrentStatus IN (0, -8, -9, -1, -2)
    BEGIN
        RAISERROR('Reject Edit not allowed for this PO status.', 16, 1);
        RETURN;
    END

    UPDATE ITPO_PurchaseOrders
    SET Status            = -8,           -- Under Revision
        PreRevisionStatus = @CurrentStatus,
        RevisionNo        = RevisionNo + 1,
        UpdatedAt         = GETDATE()
    WHERE POId = @POId;

    INSERT INTO ITPO_ApprovalHistory
        (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark)
    VALUES
        (@POId, @AdminSam, @AdminName, 'RejectEdit', @CurrentStatus, -8, @Remark);

    SELECT 1 AS Success;
END
GO

PRINT 'Created: ITPO_sp_RejectEditPO'
GO

-- ── 3. UPDATE ITPO_sp_SubmitPO ────────────────────────────────
--   เมื่อ PO อยู่ใน Status -8 (Under Revision) → ข้าม @ToStatus
--   แล้วใช้ PreRevisionStatus แทน (กลับสถานะเดิม)
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_SubmitPO]
    @POId                  INT,
    @UserSam               NVARCHAR(150),
    @SignatureBase64        NVARCHAR(MAX) = NULL,
    @SignatureImageBase64   NVARCHAR(MAX) = NULL,
    @ToStatus              INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OldStatus        INT;
    DECLARE @PreRevision      INT;
    DECLARE @ActualToStatus   INT;

    SELECT @OldStatus   = Status,
           @PreRevision = PreRevisionStatus
    FROM ITPO_PurchaseOrders
    WHERE POId = @POId;

    -- Under Revision → กลับสถานะเดิมก่อน Reject Edit
    IF @OldStatus = -8
        SET @ActualToStatus = ISNULL(@PreRevision, 1)
    ELSE
        SET @ActualToStatus = @ToStatus;

    -- อนุญาตเฉพาะ Draft (0) หรือ Under Revision (-8)
    IF @OldStatus NOT IN (0, -8)
    BEGIN
        RAISERROR('Cannot submit: PO status does not allow submission.', 16, 1);
        RETURN;
    END

    UPDATE ITPO_PurchaseOrders
    SET Status                   = @ActualToStatus,
        RequesterSignatureBase64 = @SignatureBase64,
        RequesterSignatureImage  = @SignatureImageBase64,
        RequestedDate            = GETDATE(),
        UpdatedAt                = GETDATE()
    WHERE POId = @POId;

    INSERT INTO ITPO_ApprovalHistory
        (POId, ActionBy, Action, FromStatus, ToStatus)
    VALUES
        (@POId, @UserSam,
         CASE WHEN @OldStatus = -8 THEN 'ResubmitRevision' ELSE 'Submitted' END,
         @OldStatus, @ActualToStatus);

    SELECT @@ROWCOUNT;
END
GO

PRINT '=== 17_RejectEdit.sql done ==='
GO
