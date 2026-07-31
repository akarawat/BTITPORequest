-- ============================================================
--  Script  : 15_DeptAdminRoles.sql
--  Purpose : เพิ่ม Role "ITAdmin" และ "OfficeAdmin"
--            สำหรับแยก Admin ตามแผนกในหน้า /PORequest/CreateFromPR
--
--  Role priority (สูง → ต่ำ):
--    Admin > ITAdmin > OfficeAdmin > Approver > Issuer > User
--
--  พฤติกรรม CreateFromPR:
--    Admin       → เห็น PR ทุกแผนก
--    ITAdmin     → เห็นเฉพาะ PR ที่ LinkedDeptId = 1 (IT Equipment)
--    OfficeAdmin → เห็นเฉพาะ PR ที่ LinkedDeptId = 2 (Office Stationary)
--
--  ทั้ง ITAdmin และ OfficeAdmin สามารถ:
--    - ปิด PR (Goods Received) ได้ (เฉพาะแผนกตัวเอง)
--    - ส่ง Email หา Requester ได้
--    - ดู PDF ของ PR ได้
--
--  Run on: BTITReq database
-- ============================================================

USE BTITReq;
GO

-- ── อัปเดต ITPO_sp_GetUserRole — เพิ่ม ITAdmin / OfficeAdmin ──
CREATE OR ALTER PROCEDURE [dbo].[ITPO_sp_GetUserRole]
    @SamAcc NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 RoleName
    FROM [dbo].[ITPO_UserRoles]
    WHERE SamAcc = @SamAcc
    ORDER BY CASE RoleName
        WHEN 'Admin'       THEN 1
        WHEN 'ITAdmin'     THEN 2
        WHEN 'OfficeAdmin' THEN 3
        WHEN 'Approver'    THEN 4
        WHEN 'Issuer'      THEN 5
        ELSE 6
    END
END
GO

-- ── ตรวจสอบ Role ที่มีอยู่ใน ITPO_UserRoles ────────────────
SELECT RoleName, COUNT(*) AS UserCount
FROM ITPO_UserRoles
GROUP BY RoleName
ORDER BY RoleName;
GO

-- ============================================================
-- วิธีกำหนด Role ให้ผู้ใช้ (ผ่านหน้า Admin UI หรือ SQL ตรงนี้)
--
-- ตัวอย่าง — กำหนด ITAdmin:
--   EXEC ITPO_sp_UpsertUserRole
--       @SamAcc='firstname.l', @FullName='Firstname Lastname',
--       @Email='firstname.l@berninathailand.com', @Department='IT',
--       @RoleName='ITAdmin', @CreatedBy='admin.user'
--
-- ตัวอย่าง — กำหนด OfficeAdmin:
--   EXEC ITPO_sp_UpsertUserRole
--       @SamAcc='firstname2.l', @FullName='Firstname2 Lastname2',
--       @Email='firstname2.l@berninathailand.com', @Department='HR',
--       @RoleName='OfficeAdmin', @CreatedBy='admin.user'
-- ============================================================
