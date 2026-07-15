-- ============================================================
-- Script  : 15_MonthlyDashboard.sql
-- Purpose : เพิ่ม Result Set 6 (monthly breakdown by IT/OS)
--           ใน ITPO_sp_GetDashboard
-- Run on  : BTITReq database
-- ============================================================

CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetDashboard]
    @UserSam  NVARCHAR(150) = NULL,
    @IsAdmin  BIT           = 0,
    @DeptCode NVARCHAR(50)  = NULL,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Year INT = YEAR(@DateFrom);

    -- ── (1) Summary counts ────────────────────────────────
    SELECT
        COUNT(*)                                               AS TotalPO,
        ISNULL(SUM(GrandTotal), 0)                             AS TotalAmount,
        SUM(CASE WHEN Status IN (1,2,3,4,5) THEN 1 ELSE 0 END) AS PendingCount,
        SUM(CASE WHEN Status = 6            THEN 1 ELSE 0 END) AS CompletedCount,
        SUM(CASE WHEN Status IN (-1,-2)     THEN 1 ELSE 0 END) AS RejectedCount,
        SUM(CASE WHEN Status = 0            THEN 1 ELSE 0 END) AS DraftCount
    FROM ITPO_PurchaseOrders
    WHERE
        (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
        AND PODate BETWEEN @DateFrom AND @DateTo;

    -- ── (2) Daily amounts แยก IT/OS ──────────────────────
    SELECT
        CONVERT(NVARCHAR(10), PODate, 120) AS [Date],
        CASE
            WHEN CHARINDEX('-', PONumber) > 1
            THEN LEFT(PONumber, CHARINDEX('-', PONumber) - 1)
            ELSE 'IT'
        END                                AS DeptPrefix,
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
    GROUP BY
        CONVERT(NVARCHAR(10), PODate, 120),
        CASE
            WHEN CHARINDEX('-', PONumber) > 1
            THEN LEFT(PONumber, CHARINDEX('-', PONumber) - 1)
            ELSE 'IT'
        END
    ORDER BY [Date];

    -- ── (3) Status summary ────────────────────────────────
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

    -- ── (4) Recent POs top 10 ─────────────────────────────
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

    -- ── (5) Pending my action (ไม่กรอง dept) ─────────────
    SELECT *
    FROM ITPO_PurchaseOrders
    WHERE
        (
            (Status = 1 AND IssuerSam    = @UserSam)
         OR (Status = 2 AND Approver1Sam = @UserSam)
         OR (Status = 4 AND Approver2Sam = @UserSam)
        )
    ORDER BY CreatedAt ASC;

    -- ── (6) Monthly breakdown by dept prefix for @Year ───
    --   เฉพาะ Status = 6 (Completed), ดึงตลอดปี (ไม่ขึ้นกับ DateFrom/DateTo)
    --   DeptPrefix = ส่วนแรกของ PONumber ก่อน '-' เช่น IT, OS
    SELECT
        MONTH(PODate) AS [Month],
        CASE
            WHEN CHARINDEX('-', PONumber) > 1
            THEN LEFT(PONumber, CHARINDEX('-', PONumber) - 1)
            ELSE 'IT'
        END           AS DeptPrefix,
        COUNT(*)      AS [Count],
        ISNULL(SUM(GrandTotal), 0) AS Amount
    FROM ITPO_PurchaseOrders
    WHERE
        Status = 6
        AND YEAR(PODate) = @Year
        AND (@IsAdmin = 1
            OR RequesterDeptCode = @DeptCode
            OR IssuerSam    = @UserSam
            OR Approver1Sam = @UserSam
            OR Approver2Sam = @UserSam)
    GROUP BY
        MONTH(PODate),
        CASE
            WHEN CHARINDEX('-', PONumber) > 1
            THEN LEFT(PONumber, CHARINDEX('-', PONumber) - 1)
            ELSE 'IT'
        END
    ORDER BY [Month];

END
GO

PRINT '=== 15_MonthlyDashboard.sql done — ITPO_sp_GetDashboard updated with Result Set 6 ==='
GO
