-- ============================================================
-- Script  : 14_MultiRole.sql
-- Purpose : รองรับ 1 คน มีได้หลาย Role พร้อมกัน
--           เช่น sakulchai.p เป็นทั้ง Admin และ Approver
--           1. ลบ UNIQUE constraint/index บน SamAcc (single-col)
--           2. เพิ่ม UNIQUE (SamAcc, RoleName) composite
--           3. อัปเดต SPs ทุกตัวที่เกี่ยวกับ Role
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

PRINT '=== 14_MultiRole.sql start ==='
GO

-- ── 1A. ลบ KEY CONSTRAINT (UQ) ที่คลุม SamAcc คอลัมน์เดียว ─
DECLARE @conName NVARCHAR(200)
SELECT TOP 1 @conName = kc.name
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
INNER JOIN sys.columns c
    ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE kc.parent_object_id = OBJECT_ID('ITPO_UserRoles')
  AND kc.type = 'UQ'
  AND c.name = 'SamAcc'
  AND (SELECT COUNT(*) FROM sys.index_columns ic2
       WHERE ic2.object_id = ic.object_id AND ic2.index_id = ic.index_id) = 1

IF @conName IS NOT NULL
BEGIN
    EXEC ('ALTER TABLE [dbo].[ITPO_UserRoles] DROP CONSTRAINT [' + @conName + ']')
    PRINT 'Dropped unique constraint: ' + @conName
END
ELSE
    PRINT 'No single-column key constraint on SamAcc — skipped'
GO

-- ── 1B. ลบ UNIQUE INDEX (ไม่ใช่ constraint) บน SamAcc ────
DECLARE @idxName NVARCHAR(200)
SELECT TOP 1 @idxName = i.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('ITPO_UserRoles')
  AND i.is_unique = 1 AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
  AND c.name = 'SamAcc'
  AND (SELECT COUNT(*) FROM sys.index_columns ic2
       WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id) = 1

IF @idxName IS NOT NULL
BEGIN
    EXEC ('DROP INDEX [' + @idxName + '] ON [dbo].[ITPO_UserRoles]')
    PRINT 'Dropped unique index: ' + @idxName
END
ELSE
    PRINT 'No single-column unique index on SamAcc — skipped'
GO

-- ── 2. เพิ่ม composite unique (SamAcc, RoleName) ──────────
IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID('ITPO_UserRoles')
      AND name = 'UQ_ITPO_UserRoles_SamRole'
)
BEGIN
    ALTER TABLE [dbo].[ITPO_UserRoles]
    ADD CONSTRAINT [UQ_ITPO_UserRoles_SamRole] UNIQUE ([SamAcc], [RoleName])
    PRINT 'Added composite unique (SamAcc, RoleName)'
END
ELSE
    PRINT 'Composite unique UQ_ITPO_UserRoles_SamRole already exists — skipped'
GO

-- ── 3. SP: Upsert Role — INSERT ถ้ายังไม่มี ──────────────
--    1 คนสามารถมีได้หลาย Role (Unique key บน SamAcc+RoleName)
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_UpsertUserRole]
    @SamAcc     NVARCHAR(150),
    @FullName   NVARCHAR(512)  = NULL,
    @Email      NVARCHAR(200)  = NULL,
    @Department NVARCHAR(200)  = NULL,
    @RoleName   NVARCHAR(50),
    @CreatedBy  NVARCHAR(150)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[ITPO_UserRoles]
        WHERE SamAcc = @SamAcc AND RoleName = @RoleName
    )
    BEGIN
        INSERT INTO [dbo].[ITPO_UserRoles]
            (SamAcc, FullName, Email, Department, RoleName, CreatedBy, CreatedAt, UpdatedAt)
        VALUES
            (@SamAcc, @FullName, @Email, @Department, @RoleName, @CreatedBy, GETDATE(), GETDATE())
        PRINT 'Role added: ' + @RoleName + ' for ' + @SamAcc
    END
    ELSE
    BEGIN
        -- อัปเดต info ล่าสุด (ชื่อ/email อาจเปลี่ยน) แต่ไม่เพิ่ม role ซ้ำ
        UPDATE [dbo].[ITPO_UserRoles]
        SET FullName   = ISNULL(@FullName,   FullName),
            Email      = ISNULL(@Email,      Email),
            Department = ISNULL(@Department, Department),
            UpdatedAt  = GETDATE()
        WHERE SamAcc = @SamAcc AND RoleName = @RoleName
        PRINT 'Role already exists — updated info: ' + @RoleName + ' for ' + @SamAcc
    END
END
GO

-- ── 4. SP: Delete Role — ลบทีละ Role หรือทั้งหมด ─────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_DeleteUserRole]
    @SamAcc   NVARCHAR(150),
    @RoleName NVARCHAR(50) = NULL   -- NULL = ลบทุก Role ของคนนี้
AS
BEGIN
    SET NOCOUNT ON;
    IF @RoleName IS NULL OR @RoleName = ''
    BEGIN
        DELETE FROM [dbo].[ITPO_UserRoles] WHERE SamAcc = @SamAcc
        PRINT 'All roles removed for: ' + @SamAcc
    END
    ELSE
    BEGIN
        DELETE FROM [dbo].[ITPO_UserRoles] WHERE SamAcc = @SamAcc AND RoleName = @RoleName
        PRINT 'Role ' + @RoleName + ' removed for: ' + @SamAcc
    END
END
GO

-- ── 5. SP: Get User Role — คืน Role ที่ priority สูงสุด ──
--    ใช้ที่ Login (Session) — Admin > Approver > Issuer > User
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetUserRole]
    @SamAcc NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 RoleName
    FROM [dbo].[ITPO_UserRoles]
    WHERE SamAcc = @SamAcc
    ORDER BY CASE RoleName
        WHEN 'Admin'    THEN 1
        WHEN 'Approver' THEN 2
        WHEN 'Issuer'   THEN 3
        ELSE 4
    END
END
GO

-- ── 6. SP: Get All User Roles — คืนทุก record ────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetAllUserRoles]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT *
    FROM [dbo].[ITPO_UserRoles]
    ORDER BY RoleName, FullName
END
GO

PRINT '=== 14_MultiRole.sql done ==='
GO
