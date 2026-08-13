-- ============================================================
-- Script  : 20_ResubmitRevisionResetFlow.sql
-- Purpose : เมื่อ Requester Resubmit หลัง RejectEdit
--           1. Status กลับเป็น Requested (1) เสมอ (ไม่ใช่ pre-revision)
--           2. ล้าง Issuer signature/date และ Approver signature/date
--              เพื่อให้ Issuer และ Approver เซ็นใหม่บนเนื้อหาที่แก้ไขแล้ว
-- Run on  : BTITReq database
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_SubmitPO]
    @POId                  INT,
    @UserSam               NVARCHAR(150),
    @RequesterName         NVARCHAR(512)  = NULL,
    @RequesterTitle        NVARCHAR(200)  = NULL,
    @SignatureBase64        NVARCHAR(MAX) = NULL,
    @SignatureImageBase64   NVARCHAR(MAX) = NULL,
    @ToStatus              INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OldStatus INT;
    SELECT @OldStatus = Status FROM ITPO_PurchaseOrders WHERE POId = @POId;

    -- อนุญาตเฉพาะ Draft (0) หรือ Under Revision (-8)
    IF @OldStatus NOT IN (0, -8)
    BEGIN
        RAISERROR('Cannot submit: PO status does not allow submission.', 16, 1);
        RETURN;
    END

    IF @OldStatus = -8
    BEGIN
        -- ── Revision Re-Submit: Reset Flow ──────────────────────
        -- กลับเป็น Requested (1) เสมอ ไม่ใช่ pre-revision status
        -- ล้าง Issuer และ Approver signature/date เพื่อให้เซ็นใหม่
        UPDATE ITPO_PurchaseOrders
        SET Status                   = 1,
            RequesterSignatureBase64 = @SignatureBase64,
            RequesterSignatureImage  = @SignatureImageBase64,
            RequestedDate            = GETDATE(),
            -- ล้าง Issuer (signature + identity เพื่อให้ detail fallback ไป PreAssigned)
            IssuerSam                = NULL,
            IssuerName               = NULL,
            IssuerTitle              = NULL,
            IssuerSignatureBase64    = NULL,
            IssuerSignatureImage     = NULL,
            IssuedDate               = NULL,
            -- ล้าง Approver 1 (signature + identity)
            Approver1Sam             = NULL,
            Approver1Name            = NULL,
            Approver1Title           = NULL,
            Approver1SignatureBase64 = NULL,
            Approver1SignatureImage  = NULL,
            Approver1Date            = NULL,
            Approver1Remark          = NULL,
            -- ล้าง Approver 2 (signature + identity)
            Approver2Sam             = NULL,
            Approver2Name            = NULL,
            Approver2Title           = NULL,
            Approver2SignatureBase64 = NULL,
            Approver2SignatureImage  = NULL,
            Approver2Date            = NULL,
            Approver2Remark          = NULL,
            UpdatedAt                = GETDATE()
        WHERE POId = @POId;

        INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, Action, FromStatus, ToStatus)
        VALUES (@POId, @UserSam, 'ResubmitRevision', -8, 1);
    END
    ELSE
    BEGIN
        -- ── Normal Submit: Draft → Requested ────────────────────
        UPDATE ITPO_PurchaseOrders
        SET Status                   = @ToStatus,
            RequesterSignatureBase64 = @SignatureBase64,
            RequesterSignatureImage  = @SignatureImageBase64,
            RequestedDate            = GETDATE(),
            UpdatedAt                = GETDATE()
        WHERE POId = @POId;

        INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, Action, FromStatus, ToStatus)
        VALUES (@POId, @UserSam, 'Submitted', 0, @ToStatus);
    END

    SELECT @@ROWCOUNT;
END
GO

PRINT '=== 20_ResubmitRevisionResetFlow.sql done ==='
GO
