-- ============================================================
-- Script  : UTIL_UndoCancelPO.sql
-- Purpose : Rollback PO ที่ถูก Cancel ผิดพลาด กลับสู่สถานะก่อนหน้า
-- Run on  : BTITReq database
-- Usage   : แก้ไข @TargetPONumber และ @CancelActionDate ให้ตรงกับ PO ที่ต้องการ Undo
--           แล้ว run Preview ก่อน (ROLLBACK) จากนั้นค่อย COMMIT
-- ============================================================

-- ── Step 1: ดู History ของ PO ก่อน เพื่อหา CancelActionDate ────
/*
SELECT ActionBy, ActionByName, Action, FromStatus, ToStatus, Remark, ActionDate
FROM [BTITReq].[dbo].[ITPO_ApprovalHistory]
WHERE POId = (SELECT POId FROM [BTITReq].[dbo].[ITPO_PurchaseOrders]
              WHERE PONumber = 'OS-26-00008')   -- << ใส่เลข PO
ORDER BY ActionDate DESC;
*/

-- ── Step 2: Rollback ──────────────────────────────────────────
DECLARE @TargetPONumber   NVARCHAR(50)  = 'OS-26-00008';          -- << ใส่เลข PO
DECLARE @CancelActionDate DATETIME      = '2026-08-18 10:32:51.483'; -- << จาก History ActionDate ของ Cancelled row
DECLARE @RestoreToStatus  INT           = 6;                       -- << FromStatus ของ Cancelled row (สถานะก่อนถูก Cancel)

BEGIN TRANSACTION;

DECLARE @POId INT;
SELECT @POId = POId
FROM [BTITReq].[dbo].[ITPO_PurchaseOrders]
WHERE PONumber = @TargetPONumber;

IF @POId IS NULL
BEGIN
    RAISERROR('PO Number not found.', 16, 1);
    ROLLBACK; RETURN;
END

IF (SELECT Status FROM [BTITReq].[dbo].[ITPO_PurchaseOrders] WHERE POId = @POId) != -9
BEGIN
    RAISERROR('PO is not in Cancelled state — Rollback aborted.', 16, 1);
    ROLLBACK; RETURN;
END

-- 1. คืนสถานะ
UPDATE [BTITReq].[dbo].[ITPO_PurchaseOrders]
SET Status    = @RestoreToStatus,
    UpdatedAt = GETDATE()
WHERE POId = @POId AND Status = -9;

-- 2. ลบ Cancel history entry
DELETE FROM [BTITReq].[dbo].[ITPO_ApprovalHistory]
WHERE POId     = @POId
  AND Action   = 'Cancelled'
  AND ToStatus = -9
  AND ActionDate = @CancelActionDate;

-- ── Preview ก่อน Commit ─────────────────────────────────────
SELECT PONumber, Status, UpdatedAt
FROM [BTITReq].[dbo].[ITPO_PurchaseOrders]
WHERE POId = @POId;

SELECT Action, FromStatus, ToStatus, ActionDate
FROM [BTITReq].[dbo].[ITPO_ApprovalHistory]
WHERE POId = @POId
ORDER BY ActionDate;

-- เมื่อผล Preview ถูกต้อง → เปลี่ยน ROLLBACK เป็น COMMIT TRANSACTION
ROLLBACK;
-- COMMIT TRANSACTION;
