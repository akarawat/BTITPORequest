-- ============================================================
-- BTITReq Database — ITPO Tables
-- Microsoft SQL Server 2019
-- All tables prefixed with ITPO_
-- ============================================================

USE [BTITReq]
GO

-- ──────────────────────────────────────────────────────────
-- 1. ITPO_PurchaseOrders
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'ITPO_PurchaseOrders' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ITPO_PurchaseOrders] (
        [POId]              INT            IDENTITY(1,1) NOT NULL,
        [PONumber]          NVARCHAR(30)   NOT NULL,
        [PODate]            DATE           NOT NULL,
        [InternalRefAC]     NVARCHAR(50)   NULL,
        [CreditNo]          NVARCHAR(50)   NULL,

        -- Vendor
        [VendorAttn]        NVARCHAR(200)  NOT NULL,
        [VendorCompany]     NVARCHAR(300)  NOT NULL,
        [VendorAddress]     NVARCHAR(500)  NOT NULL,
        [VendorTel]         NVARCHAR(50)   NULL,
        [VendorFax]         NVARCHAR(50)   NULL,
        [VendorEmail]       NVARCHAR(200)  NULL,
        [RefNo]             NVARCHAR(100)  NULL,
        [Subject]           NVARCHAR(500)  NOT NULL,

        -- Financials
        [Total]             DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [VatPercent]        DECIMAL(5,2)   NOT NULL DEFAULT 0,
        [VatAmount]         DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [GrandTotal]        DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [GrandTotalText]    NVARCHAR(500)  NULL,
        [Notes]             NVARCHAR(MAX)  NULL,

        -- Status:  0=Draft, 1=Requested, 2=Issued, 3=PendingApproval1,
        --          4=Authorized, 5=PendingApproval2, 6=Completed, -1/-2=Rejected
        [Status]            INT            NOT NULL DEFAULT 0,

        -- Requester
        [RequesterSam]      NVARCHAR(150)  NOT NULL,
        [RequesterName]     NVARCHAR(512)  NULL,
        [RequesterTitle]    NVARCHAR(200)  NULL,
        [RequesterSignUrl]  NVARCHAR(1000) NULL,
        [RequestedDate]     DATETIME       NULL,

        -- Issuer
        [IssuerSam]         NVARCHAR(150)  NULL,
        [IssuerName]        NVARCHAR(512)  NULL,
        [IssuerTitle]       NVARCHAR(200)  NULL,
        [IssuerSignUrl]     NVARCHAR(1000) NULL,
        [IssuedDate]        DATETIME       NULL,

        -- Approver 1
        [Approver1Sam]      NVARCHAR(150)  NULL,
        [Approver1Name]     NVARCHAR(512)  NULL,
        [Approver1Title]    NVARCHAR(200)  NULL,
        [Approver1SignUrl]  NVARCHAR(1000) NULL,
        [Approver1Date]     DATETIME       NULL,
        [Approver1Remark]   NVARCHAR(1000) NULL,

        -- Approver 2
        [Approver2Sam]      NVARCHAR(150)  NULL,
        [Approver2Name]     NVARCHAR(512)  NULL,
        [Approver2Title]    NVARCHAR(200)  NULL,
        [Approver2SignUrl]  NVARCHAR(1000) NULL,
        [Approver2Date]     DATETIME       NULL,
        [Approver2Remark]   NVARCHAR(1000) NULL,

        [CreatedAt]         DATETIME       NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]         DATETIME       NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [PK_ITPO_PurchaseOrders] PRIMARY KEY CLUSTERED ([POId] ASC),
        CONSTRAINT [UQ_ITPO_PONumber] UNIQUE ([PONumber])
    )
    PRINT 'Created table: ITPO_PurchaseOrders'
END
GO

-- ──────────────────────────────────────────────────────────
-- 2. ITPO_POLineItems
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'ITPO_POLineItems' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ITPO_POLineItems] (
        [ItemId]      INT            IDENTITY(1,1) NOT NULL,
        [POId]        INT            NOT NULL,
        [LineNo]      INT            NOT NULL,
        [Description] NVARCHAR(1000) NOT NULL,
        [Quantity]    DECIMAL(18,3)  NOT NULL,
        [UnitPrice]   DECIMAL(18,2)  NOT NULL,
        [Amount]      DECIMAL(18,2)  NOT NULL,

        CONSTRAINT [PK_ITPO_POLineItems] PRIMARY KEY CLUSTERED ([ItemId] ASC),
        CONSTRAINT [FK_ITPO_LineItems_PO] FOREIGN KEY ([POId])
            REFERENCES [ITPO_PurchaseOrders]([POId]) ON DELETE CASCADE
    )
    PRINT 'Created table: ITPO_POLineItems'
END
GO

-- ──────────────────────────────────────────────────────────
-- 3. ITPO_ApprovalHistory
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'ITPO_ApprovalHistory' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ITPO_ApprovalHistory] (
        [HistoryId]    INT            IDENTITY(1,1) NOT NULL,
        [POId]         INT            NOT NULL,
        [ActionBy]     NVARCHAR(150)  NOT NULL,
        [ActionByName] NVARCHAR(512)  NULL,
        [Action]       NVARCHAR(100)  NOT NULL,   -- Submitted, Issued, Approved, Rejected
        [Remark]       NVARCHAR(1000) NULL,
        [FromStatus]   INT            NOT NULL,
        [ToStatus]     INT            NOT NULL,
        [SignUrl]      NVARCHAR(1000) NULL,
        [ActionDate]   DATETIME       NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [PK_ITPO_ApprovalHistory] PRIMARY KEY CLUSTERED ([HistoryId] ASC),
        CONSTRAINT [FK_ITPO_History_PO] FOREIGN KEY ([POId])
            REFERENCES [ITPO_PurchaseOrders]([POId]) ON DELETE CASCADE
    )
    PRINT 'Created table: ITPO_ApprovalHistory'
END
GO

-- ──────────────────────────────────────────────────────────
-- 4. ITPO_PONumberSeq — running number
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'ITPO_PONumberSeq' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ITPO_PONumberSeq] (
        [Year]        CHAR(4)  NOT NULL,
        [LastSeq]     INT      NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ITPO_PONumberSeq] PRIMARY KEY ([Year])
    )
    PRINT 'Created table: ITPO_PONumberSeq'
END
GO

PRINT '=== Tables created successfully ==='
