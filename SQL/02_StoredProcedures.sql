-- ============================================================
-- BTITReq — ITPO Stored Procedures
-- ============================================================
USE [BTITReq]
GO

-- ──────────────────────────────────────────────────────────
-- SP: Generate PO Number  → CPS{YY}{NNNN}
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GeneratePONumber]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @year CHAR(4) = CONVERT(CHAR(4), YEAR(GETDATE()))
    DECLARE @seq  INT

    -- Upsert sequence row
    IF NOT EXISTS (SELECT 1 FROM ITPO_PONumberSeq WHERE [Year] = @year)
        INSERT INTO ITPO_PONumberSeq ([Year], LastSeq) VALUES (@year, 0)

    UPDATE ITPO_PONumberSeq
    SET @seq = LastSeq = LastSeq + 1
    WHERE [Year] = @year

    -- Format: BTPO{YY}{NNNN}   e.g. BTPO251234
    SELECT 'BTPO' + RIGHT(@year,2) + RIGHT('0000' + CAST(@seq AS VARCHAR(4)), 4)
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Create PO
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_CreatePO]
    @PONumber       NVARCHAR(30),
    @PODate         DATE,
    @InternalRefAC  NVARCHAR(50)   = NULL,
    @CreditNo       NVARCHAR(50)   = NULL,
    @VendorAttn     NVARCHAR(200),
    @VendorCompany  NVARCHAR(300),
    @VendorAddress  NVARCHAR(500),
    @VendorTel      NVARCHAR(50)   = NULL,
    @VendorFax      NVARCHAR(50)   = NULL,
    @VendorEmail    NVARCHAR(200)  = NULL,
    @RefNo          NVARCHAR(100)  = NULL,
    @Subject        NVARCHAR(500),
    @Notes          NVARCHAR(MAX)  = NULL,
    @Total          DECIMAL(18,2),
    @VatPercent     DECIMAL(5,2)   = 0,
    @VatAmount      DECIMAL(18,2)  = 0,
    @GrandTotal     DECIMAL(18,2),
    @GrandTotalText NVARCHAR(500)  = NULL,
    @RequesterSam   NVARCHAR(150),
    @Status         INT            = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ITPO_PurchaseOrders (
        PONumber, PODate, InternalRefAC, CreditNo,
        VendorAttn, VendorCompany, VendorAddress, VendorTel, VendorFax, VendorEmail,
        RefNo, Subject, Notes,
        Total, VatPercent, VatAmount, GrandTotal, GrandTotalText,
        RequesterSam, Status, CreatedAt, UpdatedAt
    ) VALUES (
        @PONumber, @PODate, @InternalRefAC, @CreditNo,
        @VendorAttn, @VendorCompany, @VendorAddress, @VendorTel, @VendorFax, @VendorEmail,
        @RefNo, @Subject, @Notes,
        @Total, @VatPercent, @VatAmount, @GrandTotal, @GrandTotalText,
        @RequesterSam, @Status, GETDATE(), GETDATE()
    )
    SELECT SCOPE_IDENTITY()
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Update PO (Draft only)
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_UpdatePO]
    @POId           INT,
    @PODate         DATE,
    @InternalRefAC  NVARCHAR(50)   = NULL,
    @CreditNo       NVARCHAR(50)   = NULL,
    @VendorAttn     NVARCHAR(200),
    @VendorCompany  NVARCHAR(300),
    @VendorAddress  NVARCHAR(500),
    @VendorTel      NVARCHAR(50)   = NULL,
    @VendorFax      NVARCHAR(50)   = NULL,
    @VendorEmail    NVARCHAR(200)  = NULL,
    @RefNo          NVARCHAR(100)  = NULL,
    @Subject        NVARCHAR(500),
    @Notes          NVARCHAR(MAX)  = NULL,
    @Total          DECIMAL(18,2),
    @VatPercent     DECIMAL(5,2)   = 0,
    @VatAmount      DECIMAL(18,2)  = 0,
    @GrandTotal     DECIMAL(18,2),
    @GrandTotalText NVARCHAR(500)  = NULL
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
        UpdatedAt = GETDATE()
    WHERE POId = @POId AND Status = 0   -- Draft only
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Upsert Line Item
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_UpsertLineItem]
    @POId        INT,
    @LineNo      INT,
    @Description NVARCHAR(1000),
    @Quantity    DECIMAL(18,3),
    @UnitPrice   DECIMAL(18,2),
    @Amount      DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ITPO_POLineItems (POId, LineNo, Description, Quantity, UnitPrice, Amount)
    VALUES (@POId, @LineNo, @Description, @Quantity, @UnitPrice, @Amount)
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Get PO by ID (returns 3 result sets)
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetPOById]
    @POId INT
AS
BEGIN
    SET NOCOUNT ON;
    -- 1. PO Header
    SELECT * FROM ITPO_PurchaseOrders WHERE POId = @POId

    -- 2. Line Items
    SELECT * FROM ITPO_POLineItems WHERE POId = @POId ORDER BY LineNo

    -- 3. Approval History
    SELECT * FROM ITPO_ApprovalHistory WHERE POId = @POId ORDER BY ActionDate DESC
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Get PO List
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetPOList]
    @UserSam  NVARCHAR(150) = NULL,
    @IsAdmin  BIT           = 0,
    @DateFrom DATE          = NULL,
    @DateTo   DATE          = NULL,
    @Status   NVARCHAR(10)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.*
    FROM ITPO_PurchaseOrders p
    WHERE
        (@IsAdmin = 1 OR p.RequesterSam = @UserSam
            OR p.IssuerSam = @UserSam
            OR p.Approver1Sam = @UserSam
            OR p.Approver2Sam = @UserSam)
        AND (@DateFrom IS NULL OR CAST(p.PODate AS DATE) >= @DateFrom)
        AND (@DateTo   IS NULL OR CAST(p.PODate AS DATE) <= @DateTo)
        AND (@Status   IS NULL OR CAST(p.Status AS NVARCHAR(10)) = @Status)
    ORDER BY p.CreatedAt DESC
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Submit PO (Draft → Requested)
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_SubmitPO]
    @POId     INT,
    @UserSam  NVARCHAR(150),
    @SignUrl  NVARCHAR(1000) = NULL,
    @ToStatus INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus INT
    SELECT @OldStatus = Status FROM ITPO_PurchaseOrders WHERE POId = @POId

    UPDATE ITPO_PurchaseOrders SET
        Status = @ToStatus,
        RequesterSignUrl = @SignUrl,
        RequestedDate = GETDATE(),
        UpdatedAt = GETDATE()
    WHERE POId = @POId

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, Action, FromStatus, ToStatus, SignUrl)
    VALUES (@POId, @UserSam, 'Submitted', @OldStatus, @ToStatus, @SignUrl)

    SELECT @@ROWCOUNT
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Issue PO (Requested → Issued)
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_IssuePO]
    @POId        INT,
    @IssuerSam   NVARCHAR(150),
    @IssuerName  NVARCHAR(512) = NULL,
    @IssuerTitle NVARCHAR(200) = NULL,
    @SignUrl     NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ITPO_PurchaseOrders SET
        Status = 2,  -- Issued
        IssuerSam = @IssuerSam,
        IssuerName = @IssuerName,
        IssuerTitle = @IssuerTitle,
        IssuerSignUrl = @SignUrl,
        IssuedDate = GETDATE(),
        UpdatedAt = GETDATE()
    WHERE POId = @POId AND Status = 1  -- only from Requested

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, SignUrl)
    VALUES (@POId, @IssuerSam, @IssuerName, 'Issued', 1, 2, @SignUrl)

    SELECT @@ROWCOUNT
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Approve PO
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_ApprovePO]
    @POId          INT,
    @Level         INT,          -- 1 or 2
    @ApproverSam   NVARCHAR(150),
    @ApproverName  NVARCHAR(512) = NULL,
    @ApproverTitle NVARCHAR(200) = NULL,
    @SignUrl       NVARCHAR(1000) = NULL,
    @Remark        NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus INT, @NewStatus INT

    IF @Level = 1
    BEGIN
        SET @OldStatus = 2  -- Issued
        SET @NewStatus = 4  -- Authorized
        UPDATE ITPO_PurchaseOrders SET
            Status = @NewStatus,
            Approver1Sam = @ApproverSam, Approver1Name = @ApproverName,
            Approver1Title = @ApproverTitle, Approver1SignUrl = @SignUrl,
            Approver1Date = GETDATE(), Approver1Remark = @Remark,
            UpdatedAt = GETDATE()
        WHERE POId = @POId AND Status = @OldStatus
    END
    ELSE IF @Level = 2
    BEGIN
        SET @OldStatus = 4  -- Authorized
        SET @NewStatus = 6  -- Completed
        UPDATE ITPO_PurchaseOrders SET
            Status = @NewStatus,
            Approver2Sam = @ApproverSam, Approver2Name = @ApproverName,
            Approver2Title = @ApproverTitle, Approver2SignUrl = @SignUrl,
            Approver2Date = GETDATE(), Approver2Remark = @Remark,
            UpdatedAt = GETDATE()
        WHERE POId = @POId AND Status = @OldStatus
    END

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, SignUrl, Remark)
    VALUES (@POId, @ApproverSam, @ApproverName,
            CASE WHEN @Level = 2 THEN 'Final Approved' ELSE 'Approved (Lvl ' + CAST(@Level AS VARCHAR) + ')' END,
            @OldStatus, @NewStatus, @SignUrl, @Remark)

    SELECT @@ROWCOUNT
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Reject PO
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_RejectPO]
    @POId         INT,
    @Level        INT,
    @ApproverSam  NVARCHAR(150),
    @ApproverName NVARCHAR(512) = NULL,
    @Remark       NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus INT
    DECLARE @NewStatus INT = CASE WHEN @Level = 2 THEN -2 ELSE -1 END

    SELECT @OldStatus = Status FROM ITPO_PurchaseOrders WHERE POId = @POId

    -- Reset back to Requested (so requester can revise)
    UPDATE ITPO_PurchaseOrders SET
        Status = @NewStatus,
        UpdatedAt = GETDATE()
    WHERE POId = @POId

    INSERT INTO ITPO_ApprovalHistory (POId, ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark)
    VALUES (@POId, @ApproverSam, @ApproverName, 'Rejected', @OldStatus, @NewStatus, @Remark)

    SELECT @@ROWCOUNT
END
GO

-- ──────────────────────────────────────────────────────────
-- SP: Dashboard Data (5 result sets)
-- ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetDashboard]
    @UserSam  NVARCHAR(150) = NULL,
    @IsAdmin  BIT           = 0,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @userId NVARCHAR(150) = @UserSam

    -- 1. Summary counts
    SELECT
        COUNT(*)                                                             AS TotalPO,
        ISNULL(SUM(GrandTotal), 0)                                           AS TotalAmount,
        SUM(CASE WHEN Status IN (1,2,3,4,5) THEN 1 ELSE 0 END)              AS PendingCount,
        SUM(CASE WHEN Status = 6 THEN 1 ELSE 0 END)                         AS CompletedCount,
        SUM(CASE WHEN Status IN (-1,-2) THEN 1 ELSE 0 END)                  AS RejectedCount,
        SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END)                         AS DraftCount
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1 OR RequesterSam = @userId OR IssuerSam = @userId
            OR Approver1Sam = @userId OR Approver2Sam = @userId)
        AND PODate BETWEEN @DateFrom AND @DateTo

    -- 2. Daily amounts
    SELECT
        CONVERT(NVARCHAR(10), PODate, 120)  AS [Date],
        SUM(GrandTotal)                     AS Amount,
        COUNT(*)                            AS [Count]
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1 OR RequesterSam = @userId OR IssuerSam = @userId
            OR Approver1Sam = @userId OR Approver2Sam = @userId)
        AND PODate BETWEEN @DateFrom AND @DateTo
    GROUP BY CONVERT(NVARCHAR(10), PODate, 120)
    ORDER BY [Date]

    -- 3. Status summary
    SELECT
        CASE Status
            WHEN 0 THEN 'Draft'       WHEN 1 THEN 'Requested'
            WHEN 2 THEN 'Issued'      WHEN 4 THEN 'Authorized'
            WHEN 6 THEN 'Completed'   WHEN -1 THEN 'Rejected'
            WHEN -2 THEN 'Rejected'   ELSE 'Other'
        END AS [Status],
        COUNT(*)         AS [Count],
        SUM(GrandTotal)  AS Amount
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1 OR RequesterSam = @userId OR IssuerSam = @userId
            OR Approver1Sam = @userId OR Approver2Sam = @userId)
        AND PODate BETWEEN @DateFrom AND @DateTo
    GROUP BY Status

    -- 4. Recent POs (top 10)
    SELECT TOP 10 *
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1 OR RequesterSam = @userId OR IssuerSam = @userId
            OR Approver1Sam = @userId OR Approver2Sam = @userId)
        AND PODate BETWEEN @DateFrom AND @DateTo
    ORDER BY CreatedAt DESC

    -- 5. Pending actions for this user
    SELECT *
    FROM ITPO_PurchaseOrders
    WHERE
        (
            (Status = 1 AND IssuerSam = @userId)            -- Waiting to issue
            OR (Status = 2 AND Approver1Sam = @userId)       -- Waiting Approver 1
            OR (Status = 4 AND Approver2Sam = @userId)       -- Waiting Approver 2
        )
    ORDER BY CreatedAt ASC
END
GO

-- ──────────────────────────────────────────────────────────
-- HR DB: Wrapper SP (run on BT_HR database)
-- ──────────────────────────────────────────────────────────
-- Note: This SP should be created on BT_HR database, not BTITReq
-- Provided here for reference — actual execution context must be BT_HR
/*
USE [BT_HR]
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ITPOgetAllSamUser]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.samacc           AS samacc,
        e.emp_code         AS emp_code,
        e.fName            AS fName,
        e.user_email       AS user_email,
        d.samacc_depmgr    AS samacc_depmgr,
        d.depmgr_email     AS depmgr_email
    FROM HR_Employees e
    LEFT JOIN HR_Departments d ON e.dept_code = d.dept_code
    WHERE e.is_active = 1
END
GO
*/

PRINT '=== All stored procedures created ==='
