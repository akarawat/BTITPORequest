-- ============================================================
-- Migration: Add digital signature + image columns
-- Run after 01_CreateTables.sql
-- ============================================================
USE [BTITReq]
GO

-- Add signature columns to ITPO_PurchaseOrders
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ITPO_PurchaseOrders') AND name='RequesterSignatureBase64')
BEGIN
    ALTER TABLE ITPO_PurchaseOrders
        ADD RequesterSignatureBase64  NVARCHAR(MAX) NULL,  -- Cryptographic (RSA-SHA256 base64)
            RequesterSignatureImage   NVARCHAR(MAX) NULL,  -- Visual image (PNG base64 from bt_signature)
            IssuerSignatureBase64     NVARCHAR(MAX) NULL,
            IssuerSignatureImage      NVARCHAR(MAX) NULL,
            Approver1SignatureBase64  NVARCHAR(MAX) NULL,
            Approver1SignatureImage   NVARCHAR(MAX) NULL,
            Approver2SignatureBase64  NVARCHAR(MAX) NULL,
            Approver2SignatureImage   NVARCHAR(MAX) NULL
    PRINT 'Signature columns added to ITPO_PurchaseOrders'
END
GO

-- Drop old SignUrl columns (replaced by SignatureBase64 + SignatureImage)
-- Uncomment below if migrating from previous version that used SignUrl
/*
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ITPO_PurchaseOrders') AND name='RequesterSignUrl')
BEGIN
    ALTER TABLE ITPO_PurchaseOrders DROP COLUMN RequesterSignUrl, IssuerSignUrl, Approver1SignUrl, Approver2SignUrl
    PRINT 'Old SignUrl columns dropped'
END
*/
GO

PRINT '=== Migration 03 complete ==='
