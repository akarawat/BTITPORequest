---- ### IT PO Reset All Signe to Null ----
UPDATE [dbo].[ITPO_PurchaseOrders]
SET IssuerSignatureBase64    = NULL,
    IssuerSignatureImage     = NULL,
    IssuedDate               = NULL,
    Approver1SignatureBase64 = NULL,
    Approver1SignatureImage  = NULL,
    Approver1Date            = NULL,
    Approver1Remark          = NULL,
    Approver2SignatureBase64 = NULL,
    Approver2SignatureImage  = NULL,
    Approver2Date            = NULL,
    Approver2Remark          = NULL,
    UpdatedAt                = GETDATE()
WHERE POId = 87
  AND Status = -8;
  -------------
UPDATE ITPO_PurchaseOrders
SET Approver1Sam   = NULL,
    Approver1Name  = NULL,
    Approver1Title = NULL,
    UpdatedAt      = GETDATE()
WHERE POId = 87
  AND Approver1Date IS NULL;
