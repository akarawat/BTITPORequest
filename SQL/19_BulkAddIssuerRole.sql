-- ============================================================
-- Script  : 19_BulkAddIssuerRole.sql
-- Purpose : กำหนด Role = 'Issuer' ให้พนักงานทุกคนใน HR DB
--           ที่ยังไม่มี Role นี้ใน ITPO_UserRoles
-- Run on  : BTITReq database (same SQL Server instance กับ BT_HR)
-- ============================================================
USE [BTITReq]
GO

-- ── Step 1: ดึงข้อมูลพนักงานทั้งหมดจาก HR SP ──────────────
IF OBJECT_ID('tempdb..#HRUsers') IS NOT NULL DROP TABLE #HRUsers
CREATE TABLE #HRUsers (
    samacc        NVARCHAR(150),
    emp_code      NVARCHAR(50),
    fName         NVARCHAR(512),
    user_email    NVARCHAR(200),
    samacc_depmgr NVARCHAR(150),
    depmgr_email  NVARCHAR(200)
)

INSERT INTO #HRUsers
EXEC [BT_HR].[dbo].[sp_ITPOgetAllSamUser]

-- ── Deduplicate: กรณี HR DB มี samacc ซ้ำ (เช่น '510') ใช้แถวแรกเท่านั้น
;WITH Deduped AS (
    SELECT samacc,
           MIN(fName)      AS fName,
           MIN(user_email) AS user_email
    FROM #HRUsers
    GROUP BY samacc
)

-- ── Step 2: Preview — ดูก่อนว่าจะเพิ่มกี่คน ────────────────
SELECT
    d.samacc,
    d.fName,
    d.user_email,
    'Issuer' AS RoleName
FROM Deduped d
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[ITPO_UserRoles] r
    WHERE r.SamAcc = d.samacc AND r.RoleName = 'Issuer'
)
ORDER BY d.fName

-- ── Step 3: INSERT Issuer role (ข้ามคนที่มีอยู่แล้ว) ────────
;WITH Deduped AS (
    SELECT samacc,
           MIN(fName)      AS fName,
           MIN(user_email) AS user_email
    FROM #HRUsers
    GROUP BY samacc
)
INSERT INTO [dbo].[ITPO_UserRoles]
    (SamAcc, FullName, Email, Department, RoleName, CreatedBy, CreatedAt, UpdatedAt)
SELECT
    d.samacc,
    d.fName,
    ISNULL(d.user_email, ''),
    '',
    'Issuer',
    'BulkScript',
    GETDATE(),
    GETDATE()
FROM Deduped d
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[ITPO_UserRoles] r
    WHERE r.SamAcc = d.samacc AND r.RoleName = 'Issuer'
)

PRINT 'Inserted ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' Issuer role(s)'

-- ── Step 4: สรุปผล ───────────────────────────────────────────
SELECT
    RoleName,
    COUNT(*) AS UserCount
FROM [dbo].[ITPO_UserRoles]
GROUP BY RoleName
ORDER BY RoleName

DROP TABLE #HRUsers
GO

PRINT '=== 19_BulkAddIssuerRole.sql done ==='
GO
