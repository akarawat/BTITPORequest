-- ============================================================
-- Update SPs to support SignatureBase64 + SignatureImage
-- ============================================================
USE [BTITReq]
GO

-- ── Submit PO ────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_SubmitPO]
    @POId                  INT,
    @UserSam               NVARCHAR(150),
    @SignatureBase64        NVARCHAR(MAX) = NULL,  -- Cryptographic
    @SignatureImageBase64   NVARCHAR(MAX) = NULL,  -- Visual image
    @ToStatus              INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus INT
    SELECT @OldStatus = Status FROM ITPO_PurchaseOrders WHERE POId = @POId

    UPDATE ITPO_PurchaseOrders SET
        Status = @ToStatus,
        RequesterSignatureBase64 = @SignatureBase64,
        RequesterSignatureImage  = @SignatureImageBase64,
        RequestedDate = GETDATE(),
        UpdatedAt = GETDATE()
    WHERE POId = @POId

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, Action, FromStatus, ToStatus)
    VALUES (@POId, @UserSam, 'Submitted', @OldStatus, @ToStatus)

    SELECT @@ROWCOUNT
END
GO

-- ── Issue PO ─────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_IssuePO]
    @POId                  INT,
    @IssuerSam             NVARCHAR(150),
    @IssuerName            NVARCHAR(512) = NULL,
    @IssuerTitle           NVARCHAR(200) = NULL,
    @SignatureBase64        NVARCHAR(MAX) = NULL,
    @SignatureImageBase64   NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ITPO_PurchaseOrders SET
        Status = 2,
        IssuerSam = @IssuerSam, IssuerName = @IssuerName, IssuerTitle = @IssuerTitle,
        IssuerSignatureBase64 = @SignatureBase64,
        IssuerSignatureImage  = @SignatureImageBase64,
        IssuedDate = GETDATE(), UpdatedAt = GETDATE()
    WHERE POId = @POId AND Status = 1

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus)
    VALUES (@POId, @IssuerSam, @IssuerName, 'Issued', 1, 2)

    SELECT @@ROWCOUNT
END
GO

-- ── Approve PO ───────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_ApprovePO]
    @POId                  INT,
    @Level                 INT,
    @ApproverSam           NVARCHAR(150),
    @ApproverName          NVARCHAR(512) = NULL,
    @ApproverTitle         NVARCHAR(200) = NULL,
    @SignatureBase64        NVARCHAR(MAX) = NULL,
    @SignatureImageBase64   NVARCHAR(MAX) = NULL,
    @Remark                NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus INT, @NewStatus INT

    IF @Level = 1
    BEGIN
        SET @OldStatus = 2; SET @NewStatus = 4
        UPDATE ITPO_PurchaseOrders SET
            Status = @NewStatus,
            Approver1Sam = @ApproverSam, Approver1Name = @ApproverName, Approver1Title = @ApproverTitle,
            Approver1SignatureBase64 = @SignatureBase64,
            Approver1SignatureImage  = @SignatureImageBase64,
            Approver1Date = GETDATE(), Approver1Remark = @Remark,
            UpdatedAt = GETDATE()
        WHERE POId = @POId AND Status = @OldStatus
    END
    ELSE IF @Level = 2
    BEGIN
        SET @OldStatus = 4; SET @NewStatus = 6
        UPDATE ITPO_PurchaseOrders SET
            Status = @NewStatus,
            Approver2Sam = @ApproverSam, Approver2Name = @ApproverName, Approver2Title = @ApproverTitle,
            Approver2SignatureBase64 = @SignatureBase64,
            Approver2SignatureImage  = @SignatureImageBase64,
            Approver2Date = GETDATE(), Approver2Remark = @Remark,
            UpdatedAt = GETDATE()
        WHERE POId = @POId AND Status = @OldStatus
    END

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark)
    VALUES (@POId, @ApproverSam, @ApproverName,
        CASE WHEN @Level=2 THEN 'Final Approved' ELSE 'Approved' END,
        @OldStatus, @NewStatus, @Remark)

    SELECT @@ROWCOUNT
END
GO

PRINT '=== SPs updated with signature columns ==='
