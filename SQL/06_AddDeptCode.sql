-- ============================================================
-- Script  : 06_AddDeptCode.sql
-- Purpose : เพิ่ม RequesterDeptCode ใน ITPO_PurchaseOrders
--           เพื่อ filter Dashboard ตามแผนก
-- Run on  : 1) BTITReq database (ส่วนใหญ่)
--           2) BT_HR database (อัปเดต sp_ITPOgetAllSamUser)
-- ============================================================

-- ══════════════════════════════════════════════════════
-- PART A: BTITReq database
-- ══════════════════════════════════════════════════════

-- ── 1. เพิ่ม Column RequesterDeptCode ─────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ITPO_PurchaseOrders')
      AND name = 'RequesterDeptCode'
)
BEGIN
    ALTER TABLE [dbo].[ITPO_PurchaseOrders]
    ADD [RequesterDeptCode] NVARCHAR(50) NULL;

    -- Index เพื่อ query เร็ว
    CREATE INDEX IX_ITPO_PO_DeptCode ON [dbo].[ITPO_PurchaseOrders] ([RequesterDeptCode]);
    PRINT 'Column RequesterDeptCode added.';
END
ELSE
    PRINT 'Column RequesterDeptCode already exists — skipped.';
GO

-- ── 2. อัปเดต ITPO_sp_CreatePO รับ RequesterDeptCode ──
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_CreatePO]
    @PONumber       NVARCHAR(50),
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
    @RequesterSam       NVARCHAR(150),
    @RequesterDeptCode  NVARCHAR(50)  = NULL,   -- NEW
    @PreAssignedIssuerSam    NVARCHAR(150) = NULL,
    @PreAssignedApprover1Sam NVARCHAR(150) = NULL,
    @PreAssignedApprover2Sam NVARCHAR(150) = NULL,
    @Status         INT            = 0
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
        Status, CreatedAt, UpdatedAt
    ) VALUES (
        @PONumber, @PODate, @InternalRefAC, @CreditNo,
        @VendorAttn, @VendorCompany, @VendorAddress, @VendorTel, @VendorFax, @VendorEmail,
        @RefNo, @Subject, @Notes,
        @Total, @VatPercent, @VatAmount, @GrandTotal, @GrandTotalText,
        @RequesterSam, @RequesterDeptCode,
        @PreAssignedIssuerSam, @PreAssignedApprover1Sam, @PreAssignedApprover2Sam,
        @Status, GETDATE(), GETDATE()
    );
    SELECT SCOPE_IDENTITY();
END
GO

-- ── 3. อัปเดต ITPO_sp_GetDashboard filter by DeptCode ─
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetDashboard]
    @UserSam  NVARCHAR(150) = NULL,
    @IsAdmin  BIT           = 0,
    @DeptCode NVARCHAR(50)  = NULL,   -- NEW: NULL = ไม่กรอง (Admin)
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Summary counts (1)
    SELECT
        COUNT(*)                                                              AS TotalPO,
        ISNULL(SUM(GrandTotal), 0)                                            AS TotalAmount,
        SUM(CASE WHEN Status IN (1,2,3,4,5) THEN 1 ELSE 0 END)               AS PendingCount,
        SUM(CASE WHEN Status = 6           THEN 1 ELSE 0 END)                 AS CompletedCount,
        SUM(CASE WHEN Status IN (-1,-2)    THEN 1 ELSE 0 END)                 AS RejectedCount,
        SUM(CASE WHEN Status = 0           THEN 1 ELSE 0 END)                 AS DraftCount
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
        AND PODate BETWEEN @DateFrom AND @DateTo;

    -- Daily amounts (2)
    SELECT
        CONVERT(NVARCHAR(10), PODate, 120) AS [Date],
        SUM(GrandTotal)                    AS Amount,
        COUNT(*)                           AS [Count]
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
        AND PODate BETWEEN @DateFrom AND @DateTo
    GROUP BY CONVERT(NVARCHAR(10), PODate, 120)
    ORDER BY [Date];

    -- Status summary (3)
    SELECT
        CASE Status
            WHEN  0 THEN 'Draft'      WHEN  1 THEN 'Requested'
            WHEN  2 THEN 'Issued'     WHEN  4 THEN 'Authorized'
            WHEN  6 THEN 'Completed'  WHEN -1 THEN 'Rejected'
            WHEN -2 THEN 'Rejected'   ELSE 'Other'
        END AS [Status],
        COUNT(*)        AS [Count],
        SUM(GrandTotal) AS Amount
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
        AND PODate BETWEEN @DateFrom AND @DateTo
    GROUP BY Status;

    -- Recent POs top 10 (4)
    SELECT TOP 10 *
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
        AND PODate BETWEEN @DateFrom AND @DateTo
    ORDER BY CreatedAt DESC;

    -- Pending my action (5) — ไม่กรอง dept, ดูตาม assignment เท่านั้น
    SELECT *
    FROM ITPO_PurchaseOrders
    WHERE
        (
            (Status = 1 AND IssuerSam    = @UserSam)
         OR (Status = 2 AND Approver1Sam = @UserSam)
         OR (Status = 4 AND Approver2Sam = @UserSam)
        )
    ORDER BY CreatedAt ASC;
END
GO

PRINT '=== PART A (BTITReq) done ==='
GO

-- ══════════════════════════════════════════════════════
-- PART B: BT_HR database — อัปเดต sp_ITPOgetAllSamUser
--         ให้ return dep_code ด้วย
-- รัน script นี้บน BT_HR database แยกต่างหาก
-- ══════════════════════════════════════════════════════
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
        e.dept_code        AS dep_code,       -- NEW: เพิ่ม dept_code
        d.samacc_depmgr    AS samacc_depmgr,
        d.depmgr_email     AS depmgr_email
    FROM HR_Employees e
    LEFT JOIN HR_Departments d ON e.dept_code = d.dept_code
    WHERE e.is_active = 1
END
GO

PRINT '=== PART B (BT_HR) done ==='
GO
*/
