-- ============================================================
-- Script  : 18_FixSPsForRevision.sql
-- Purpose : แก้ SP 2 ตัวที่ถูก overwrite โดย 17_RejectEdit.sql
--           1. ITPO_sp_SubmitPO  — เพิ่ม @RequesterName/@RequesterTitle กลับ
--                                   + logic UnderRevision (-8) ยังคงอยู่
--           2. ITPO_sp_UpdatePO  — อนุญาต Status IN (0, -8)
-- Run on  : BTITReq database
-- ============================================================

-- ── 1. ITPO_sp_SubmitPO ──────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_SubmitPO]
    @POId                  INT,
    @UserSam               NVARCHAR(150),
    @RequesterName         NVARCHAR(512)  = NULL,   -- รับ param แต่ไม่ต้อง update (set ตอน Create แล้ว)
    @RequesterTitle        NVARCHAR(200)  = NULL,   -- รับ param แต่ไม่ต้อง update
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

PRINT 'Updated: ITPO_sp_SubmitPO (RequesterName/Title params restored + UnderRevision logic)'
GO

-- ── 2. ITPO_sp_UpdatePO ──────────────────────────────────────
--   อนุญาต Status IN (0, -8) — Draft และ Under Revision แก้ไขได้
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_UpdatePO]
    @POId            INT,
    @PODate          DATE,
    @InternalRefAC   NVARCHAR(50)   = NULL,
    @CreditNo        NVARCHAR(50)   = NULL,
    @VendorAttn      NVARCHAR(200),
    @VendorCompany   NVARCHAR(300),
    @VendorAddress   NVARCHAR(500),
    @VendorTel       NVARCHAR(50)   = NULL,
    @VendorFax       NVARCHAR(50)   = NULL,
    @VendorEmail     NVARCHAR(200)  = NULL,
    @RefNo           NVARCHAR(100)  = NULL,
    @Subject         NVARCHAR(500),
    @Notes           NVARCHAR(MAX)  = NULL,
    @Total           DECIMAL(18,2),
    @VatPercent      DECIMAL(5,2)   = 0,
    @VatAmount       DECIMAL(18,2)  = 0,
    @GrandTotal      DECIMAL(18,2),
    @GrandTotalText  NVARCHAR(500)  = NULL,
    @OldPONumber     NVARCHAR(50)   = NULL,
    @InternalContact NVARCHAR(200)  = NULL,
    @WorkOrderNo     NVARCHAR(50)   = NULL,
    @NCRNo           NVARCHAR(50)   = NULL,
    @IRCRNo          NVARCHAR(50)   = NULL,
    @ChangeNo        NVARCHAR(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ITPO_PurchaseOrders SET
        PODate = @PODate, InternalRefAC = @InternalRefAC, CreditNo = @CreditNo,
        VendorAttn = @VendorAttn, VendorCompany = @VendorCompany,
        VendorAddress = @VendorAddress, VendorTel = @VendorTel, VendorFax = @VendorFax,
        VendorEmail = @VendorEmail, RefNo = @RefNo, Subject = @Subject, Notes = @Notes,
        Total = @Total, VatPercent = @VatPercent, VatAmount = @VatAmount,
        GrandTotal = @GrandTotal, GrandTotalText = @GrandTotalText,
        OldPONumber = @OldPONumber, InternalContact = @InternalContact,
        WorkOrderNo = @WorkOrderNo, NCRNo = @NCRNo, IRCRNo = @IRCRNo, ChangeNo = @ChangeNo,
        UpdatedAt = GETDATE()
    WHERE POId = @POId AND Status IN (0, -8)   -- Draft หรือ Under Revision
END
GO

PRINT 'Updated: ITPO_sp_UpdatePO (Status IN (0,-8))'
GO

PRINT '=== 18_FixSPsForRevision.sql done ==='
GO
