-- ============================================================
-- Script  : 13_ExtraFields.sql
-- Purpose : เพิ่ม reference fields สำหรับ backward-compat กับระบบเดิม
--           1. OldPONumber     — Ref. Old PO No.
--           2. InternalContact — Internal Contact
--           3. WorkOrderNo     — Work Order No.
--           4. NCRNo           — NCR No.
--           5. IRCRNo          — IR/CR No.
--           6. ChangeNo        — Change No.
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

-- ── 1. เพิ่มคอลัมน์ใน ITPO_PurchaseOrders ────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'OldPONumber')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [OldPONumber]     NVARCHAR(50)  NULL
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'InternalContact')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [InternalContact] NVARCHAR(200) NULL
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'WorkOrderNo')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [WorkOrderNo]     NVARCHAR(50)  NULL
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'NCRNo')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [NCRNo]           NVARCHAR(50)  NULL
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'IRCRNo')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [IRCRNo]          NVARCHAR(50)  NULL
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders') AND name = 'ChangeNo')
    ALTER TABLE [dbo].[ITPO_PurchaseOrders] ADD [ChangeNo]        NVARCHAR(50)  NULL
GO
PRINT 'Columns added to ITPO_PurchaseOrders'
GO

-- ── 2. อัปเดต SP: Create PO ───────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_CreatePO]
    @PONumber                NVARCHAR(30),
    @PODate                  DATE,
    @InternalRefAC           NVARCHAR(50)   = NULL,
    @CreditNo                NVARCHAR(50)   = NULL,
    @VendorAttn              NVARCHAR(200),
    @VendorCompany           NVARCHAR(300),
    @VendorAddress           NVARCHAR(500),
    @VendorTel               NVARCHAR(50)   = NULL,
    @VendorFax               NVARCHAR(50)   = NULL,
    @VendorEmail             NVARCHAR(200)  = NULL,
    @RefNo                   NVARCHAR(100)  = NULL,
    @Subject                 NVARCHAR(500),
    @Notes                   NVARCHAR(MAX)  = NULL,
    @Total                   DECIMAL(18,2),
    @VatPercent              DECIMAL(5,2)   = 0,
    @VatAmount               DECIMAL(18,2)  = 0,
    @GrandTotal              DECIMAL(18,2),
    @GrandTotalText          NVARCHAR(500)  = NULL,
    @RequesterSam            NVARCHAR(150),
    @RequesterDeptCode       NVARCHAR(50)   = NULL,
    @PreAssignedIssuerSam    NVARCHAR(150)  = NULL,
    @PreAssignedApprover1Sam NVARCHAR(150)  = NULL,
    @PreAssignedApprover2Sam NVARCHAR(150)  = NULL,
    @Status                  INT            = 0,
    -- New reference fields
    @OldPONumber             NVARCHAR(50)   = NULL,
    @InternalContact         NVARCHAR(200)  = NULL,
    @WorkOrderNo             NVARCHAR(50)   = NULL,
    @NCRNo                   NVARCHAR(50)   = NULL,
    @IRCRNo                  NVARCHAR(50)   = NULL,
    @ChangeNo                NVARCHAR(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ITPO_PurchaseOrders (
        PONumber, PODate, InternalRefAC, CreditNo,
        VendorAttn, VendorCompany, VendorAddress, VendorTel, VendorFax, VendorEmail,
        RefNo, Subject, Notes,
        Total, VatPercent, VatAmount, GrandTotal, GrandTotalText,
        RequesterSam, RequesterDeptCode,
        PreAssignedIssuerSam, PreAssignedApprover1Sam, PreAssignedApprover2Sam,
        Status,
        OldPONumber, InternalContact, WorkOrderNo, NCRNo, IRCRNo, ChangeNo,
        CreatedAt, UpdatedAt
    ) VALUES (
        @PONumber, @PODate, @InternalRefAC, @CreditNo,
        @VendorAttn, @VendorCompany, @VendorAddress, @VendorTel, @VendorFax, @VendorEmail,
        @RefNo, @Subject, @Notes,
        @Total, @VatPercent, @VatAmount, @GrandTotal, @GrandTotalText,
        @RequesterSam, @RequesterDeptCode,
        @PreAssignedIssuerSam, @PreAssignedApprover1Sam, @PreAssignedApprover2Sam,
        @Status,
        @OldPONumber, @InternalContact, @WorkOrderNo, @NCRNo, @IRCRNo, @ChangeNo,
        GETDATE(), GETDATE()
    )
    SELECT SCOPE_IDENTITY()
END
GO

-- ── 3. อัปเดต SP: Update PO (Draft only) ──────────────────────
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
    -- New fields
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
    WHERE POId = @POId AND Status = 0   -- Draft only
END
GO

PRINT '=== 13_ExtraFields.sql done ==='
GO
