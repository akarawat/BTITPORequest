-- ============================================================
-- Script  : 09_PONumberPrefix.sql
-- Purpose : เพิ่ม Running No. แยกตาม DeptPrefix (IT / OS)
--           Format: IT-YY-00000 / OS-YY-00000
-- Run on  : BTITReq database
-- ============================================================
USE [BTITReq]
GO

-- ── 1. สร้างตาราง ITPO_PONumberSeqByDept ─────────────────────
--       แยก sequence ต่อ (DeptPrefix, Year)
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE name = 'ITPO_PONumberSeqByDept' AND type = 'U'
)
BEGIN
    CREATE TABLE [dbo].[ITPO_PONumberSeqByDept] (
        [DeptPrefix] CHAR(2)  NOT NULL,   -- 'IT' หรือ 'OS'
        [Year]       CHAR(2)  NOT NULL,   -- 2 หลักท้าย เช่น '25'
        [LastSeq]    INT      NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ITPO_PONumberSeqByDept] PRIMARY KEY ([DeptPrefix], [Year])
    )
    PRINT 'Created table: ITPO_PONumberSeqByDept'
END
ELSE
    PRINT 'Table ITPO_PONumberSeqByDept already exists — skipped.'
GO

-- ── 2. อัปเดต ITPO_sp_GeneratePONumber ───────────────────────
--       รับ @DeptPrefix ('IT'|'OS', default 'IT')
--       Return: IT-25-00001 / OS-25-00001
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GeneratePONumber]
    @DeptPrefix CHAR(2) = 'IT'
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate prefix
    IF @DeptPrefix NOT IN ('IT', 'OS')
        SET @DeptPrefix = 'IT'

    DECLARE @year CHAR(2) = RIGHT(CONVERT(CHAR(4), YEAR(GETDATE())), 2)
    DECLARE @seq  INT

    -- Upsert sequence row (row-level lock กัน race condition)
    IF NOT EXISTS (
        SELECT 1 FROM ITPO_PONumberSeqByDept
        WHERE DeptPrefix = @DeptPrefix AND [Year] = @year
    )
        INSERT INTO ITPO_PONumberSeqByDept (DeptPrefix, [Year], LastSeq)
        VALUES (@DeptPrefix, @year, 0)

    UPDATE ITPO_PONumberSeqByDept WITH (UPDLOCK, SERIALIZABLE)
    SET @seq = LastSeq = LastSeq + 1
    WHERE DeptPrefix = @DeptPrefix AND [Year] = @year

    -- Format: IT-25-00001  (5 หลัก)
    SELECT @DeptPrefix + '-' + @year + '-' + RIGHT('00000' + CAST(@seq AS VARCHAR(5)), 5)
END
GO

PRINT '=== 09_PONumberPrefix.sql done ==='
GO
