-- ============================================================
-- Script  : 05_EmailLogs.sql
-- Purpose : สร้างตาราง ITPO_EmailLogs สำหรับเก็บ log การส่งอีเมล์
-- Run on  : BTITReq database (same DB as PO system)
-- ============================================================

-- ── 1. สร้างตาราง ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ITPO_EmailLogs')
BEGIN
    CREATE TABLE [dbo].[ITPO_EmailLogs] (
        [LogId]       INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [SentAt]      DATETIME      NOT NULL DEFAULT GETDATE(),
        [ToEmail]     NVARCHAR(500) NOT NULL,
        [Subject]     NVARCHAR(500) NOT NULL,
        [PONumber]    NVARCHAR(50)  NULL,
        [POId]        INT           NULL,
        [MailType]    NVARCHAR(100) NULL,
        [IsSuccess]   BIT           NOT NULL DEFAULT 0,
        [HttpStatus]  INT           NULL,
        [ErrorMsg]    NVARCHAR(MAX) NULL,
        [IsDebug]     BIT           NOT NULL DEFAULT 0,
        [OriginalTo]  NVARCHAR(500) NULL,
        [CreatedBy]   NVARCHAR(100) NULL
    );
    CREATE INDEX IX_ITPO_EmailLogs_POId   ON [dbo].[ITPO_EmailLogs] ([POId]);
    CREATE INDEX IX_ITPO_EmailLogs_SentAt ON [dbo].[ITPO_EmailLogs] ([SentAt] DESC);
END
GO

-- ── 2. SP Insert ───────────────────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_InsertEmailLog]
    @ToEmail    NVARCHAR(500),
    @Subject    NVARCHAR(500),
    @PONumber   NVARCHAR(50)  = NULL,
    @POId       INT           = NULL,
    @MailType   NVARCHAR(100) = NULL,
    @IsSuccess  BIT           = 0,
    @HttpStatus INT           = NULL,
    @ErrorMsg   NVARCHAR(MAX) = NULL,
    @IsDebug    BIT           = 0,
    @OriginalTo NVARCHAR(500) = NULL,
    @CreatedBy  NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ITPO_EmailLogs
        (ToEmail, Subject, PONumber, POId, MailType, IsSuccess, HttpStatus, ErrorMsg, IsDebug, OriginalTo, CreatedBy)
    VALUES
        (@ToEmail, @Subject, @PONumber, @POId, @MailType, @IsSuccess, @HttpStatus, @ErrorMsg, @IsDebug, @OriginalTo, @CreatedBy);
END
GO

-- ── 3. SP Query (Admin report) ─────────────────────────────
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetEmailLogs]
    @DateFrom  DATETIME      = NULL,
    @DateTo    DATETIME      = NULL,
    @PONumber  NVARCHAR(50)  = NULL,
    @MailType  NVARCHAR(100) = NULL,
    @IsSuccess BIT           = NULL,
    @PageNum   INT           = 1,
    @PageSize  INT           = 50
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalCount
    FROM ITPO_EmailLogs
    WHERE
        (@DateFrom  IS NULL OR SentAt   >= @DateFrom)
        AND (@DateTo    IS NULL OR SentAt   <= DATEADD(DAY,1,@DateTo))
        AND (@PONumber  IS NULL OR PONumber LIKE '%' + @PONumber + '%')
        AND (@MailType  IS NULL OR MailType  = @MailType)
        AND (@IsSuccess IS NULL OR IsSuccess = @IsSuccess);

    SELECT *
    FROM ITPO_EmailLogs
    WHERE
        (@DateFrom  IS NULL OR SentAt   >= @DateFrom)
        AND (@DateTo    IS NULL OR SentAt   <= DATEADD(DAY,1,@DateTo))
        AND (@PONumber  IS NULL OR PONumber LIKE '%' + @PONumber + '%')
        AND (@MailType  IS NULL OR MailType  = @MailType)
        AND (@IsSuccess IS NULL OR IsSuccess = @IsSuccess)
    ORDER BY SentAt DESC
    OFFSET ((@PageNum - 1) * @PageSize) ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
