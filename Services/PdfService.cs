using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Events;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace BTITPORequest.Services
{
    public class PdfService : IPdfService
    {
        private readonly IConfiguration _config;
        private readonly IDigitalSignService _signService;
        private readonly ILogger<PdfService> _logger;

        private static readonly DeviceRgb ColorHeader = new(0x1A, 0x56, 0x76);
        private static readonly DeviceRgb ColorHeaderText = new(0xFF, 0xFF, 0xFF);
        private static readonly DeviceRgb ColorRowAlt = new(0xF0, 0xF7, 0xFB);
        private static readonly DeviceRgb ColorBorder = new(0xCC, 0xCC, 0xCC);
        private static readonly DeviceRgb ColorSignedGreen = new(0x28, 0x7A, 0x4C);

        public PdfService(IConfiguration config, IDigitalSignService signService, ILogger<PdfService> logger)
        {
            _config = config;
            _signService = signService;
            _logger = logger;
        }

        // ── Thai-capable font loader ────────────────────────────
        // HELVETICA รองรับแค่ Latin — ถ้า vendor มีภาษาไทย จะไม่แสดง
        // ใช้ Tahoma (มีใน Windows ทุกเครื่อง, รองรับไทย) แทน
        private static readonly string[] FontPathsRegular = {
            @"C:\Windows\Fonts\tahoma.ttf",
            @"C:\Windows\Fonts\arial.ttf",
        };
        private static readonly string[] FontPathsBold = {
            @"C:\Windows\Fonts\tahomabd.ttf",
            @"C:\Windows\Fonts\arialbd.ttf",
        };

        private static PdfFont LoadThaiFont(string[] paths, string fallback)
        {
            foreach (var p in paths)
            {
                if (System.IO.File.Exists(p))
                    return PdfFontFactory.CreateFont(p, PdfEncodings.IDENTITY_H,
                           PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
            }
            return PdfFontFactory.CreateFont(fallback);
        }

        public async Task<byte[]> GeneratePOPdfAsync(PORequestModel po, string signerUsername, string signerFullName)
        {
            // 1. สร้าง PDF ด้วย iText7
            var rawPdf = BuildPdf(po);

            // 2. ส่งไป embed digital signature ที่ BTDigitalSign API
            var signedPdf = await _signService.SignPdfAsync(
                rawPdf,
                documentName: $"PO_{po.PONumber}",
                referenceId: po.PONumber,
                signerUsername: signerUsername,
                signerFullName: signerFullName,
                signerRole: "PO Requester",
                signPage: 1,
                x: 36f, y: 36f, width: 180f, height: 50f);

            return signedPdf ?? rawPdf;
        }

        // ── Build raw PDF with iText7 ─────────────────────────
        private byte[] BuildPdf(PORequestModel po)
        {
            var appSettings = _config.GetSection("AppSettings");

            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf, PageSize.A4);
            // Margin: บน 2.2cm (รูปหัว ~1.7cm + gap), ล่าง 2.5cm (รูปท้าย ~1.5cm + gap)
            doc.SetMargins(62f, 30f, 72f, 30f);

            // ใช้ Tahoma (รองรับภาษาไทย) แทน Helvetica
            var fontR = LoadThaiFont(FontPathsRegular, StandardFonts.HELVETICA);
            var fontB = LoadThaiFont(FontPathsBold, StandardFonts.HELVETICA_BOLD);

            // ── Header & Footer images via Page Event ─────────────
            var imgLogoPath = System.IO.Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "img", "bernina_letterhead_logo.png");
            var imgSwissPath = System.IO.Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "img", "swiss_heritage_logo.png");

            pdf.AddEventHandler(PdfDocumentEvent.END_PAGE,
                new LetterheadPageEvent(imgLogoPath, imgSwissPath));

            // ── PO Title ──────────────────────────────────────
            var title = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth().SetMarginBottom(6);
            var tL = new Cell().SetBorder(Border.NO_BORDER);
            tL.Add(new Paragraph("PURCHASE ORDER No.").SetFont(fontB).SetFontSize(10));
            tL.Add(new Paragraph(po.PONumber).SetFont(fontB).SetFontSize(14).SetFontColor(ColorHeader));
            title.AddCell(tL);
            var tR = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            tR.Add(new Paragraph($"Date:  {po.PODate:M/d/yyyy}").SetFont(fontR).SetFontSize(9));
            if (!string.IsNullOrEmpty(po.InternalRefAC))
                tR.Add(new Paragraph($"For BT. Internal Ref.: A/C   {po.InternalRefAC}").SetFont(fontR).SetFontSize(9));
            if (!string.IsNullOrEmpty(po.CreditNo))
                tR.Add(new Paragraph($"Credit No:  {po.CreditNo}").SetFont(fontR).SetFontSize(9));
            title.AddCell(tR);
            doc.Add(title);

            // ── Vendor ────────────────────────────────────────
            void LabelVal(string label, string val) =>
                doc.Add(new Paragraph().SetFont(fontR).SetFontSize(9).SetMarginBottom(2)
                    .Add(new Text(label + "  ").SetFont(fontB))
                    .Add(new Text(val)));

            LabelVal("Attn:", po.VendorAttn);
            LabelVal("Company:", po.VendorCompany);
            LabelVal("Address:", po.VendorAddress);
            LabelVal("Tel No.:", po.VendorTel + "     Fax No.:  " + po.VendorFax);
            LabelVal("E-mail:", po.VendorEmail);
            LabelVal("Ref.:", po.RefNo);

            // ── Reference fields (optional) ──────────────────
            bool hasRef2 = !string.IsNullOrEmpty(po.InternalContact) || !string.IsNullOrEmpty(po.OldPONumber)
                        || !string.IsNullOrEmpty(po.WorkOrderNo)     || !string.IsNullOrEmpty(po.NCRNo)
                        || !string.IsNullOrEmpty(po.IRCRNo)          || !string.IsNullOrEmpty(po.ChangeNo);
            if (hasRef2)
            {
                var refTbl2 = new Table(UnitValue.CreatePercentArray(new float[] { 33, 33, 34 }))
                    .UseAllAvailableWidth().SetMarginBottom(2);

                void RefCell2(string label, string? val)
                {
                    var c = new Cell().SetBorder(Border.NO_BORDER);
                    if (!string.IsNullOrEmpty(val))
                        c.Add(new Paragraph().SetFont(fontR).SetFontSize(8.5f).SetMarginBottom(1)
                            .Add(new Text(label + "  ").SetFont(fontB))
                            .Add(new Text(val)));
                    else
                        c.Add(new Paragraph(" ").SetFont(fontR).SetFontSize(8.5f));
                    refTbl2.AddCell(c);
                }

                RefCell2("Contact:", po.InternalContact);
                RefCell2("Ref. Old PO:", po.OldPONumber);
                RefCell2("", null);
                RefCell2("WO No.:", po.WorkOrderNo);
                RefCell2("NCR No.:", po.NCRNo);
                RefCell2("IR/CR No.:", po.IRCRNo);
                if (!string.IsNullOrEmpty(po.ChangeNo))
                {
                    var cc2 = new Cell(1, 3).SetBorder(Border.NO_BORDER);
                    cc2.Add(new Paragraph().SetFont(fontR).SetFontSize(8.5f).SetMarginBottom(1)
                        .Add(new Text("Change No.:  ").SetFont(fontB))
                        .Add(new Text(po.ChangeNo)));
                    refTbl2.AddCell(cc2);
                }
                doc.Add(refTbl2);
            }

            doc.Add(new Paragraph($"Subject:  {po.Subject}").SetFont(fontR).SetFontSize(9).SetMarginBottom(8));

            // ── Line Items ────────────────────────────────────
            var tbl = new Table(UnitValue.CreatePercentArray(new float[] { 5, 50, 15, 15, 15 }))
                .UseAllAvailableWidth().SetMarginBottom(6);
            foreach (var h in new[] { "#", "Description", "Quantity", "Unit Price", "Amount/Baht" })
                tbl.AddHeaderCell(new Cell().SetBackgroundColor(ColorHeader).SetBorder(Border.NO_BORDER)
                    .Add(new Paragraph(h).SetFont(fontB).SetFontSize(9).SetFontColor(ColorHeaderText).SetTextAlignment(TextAlignment.CENTER)));

            bool alt = false;
            foreach (var item in po.LineItems)
            {
                var bg = alt ? ColorRowAlt : null;
                void AddCell(string val, bool right = false)
                {
                    var c = new Cell().SetBorder(Border.NO_BORDER)
                        .SetBorderBottom(new SolidBorder(ColorBorder, 0.5f));
                    if (bg != null) c.SetBackgroundColor(bg);
                    c.Add(new Paragraph(val).SetFont(fontR).SetFontSize(9)
                        .SetTextAlignment(right ? TextAlignment.RIGHT : TextAlignment.LEFT));
                    tbl.AddCell(c);
                }
                AddCell(item.LineNo.ToString());
                AddCell(item.Description);
                AddCell(item.Quantity.ToString("N3"), true);
                AddCell(item.UnitPrice.ToString("N2"), true);
                AddCell(item.Amount.ToString("N2"), true);
                alt = !alt;
            }
            doc.Add(tbl);

            // ── Totals — จัดให้ตรงกับ column Amount (14%) ────────
            var totals = new Table(UnitValue.CreatePercentArray(new float[] { 71, 15, 14 })).UseAllAvailableWidth().SetMarginBottom(4);
            void TotalRow(string label, string val, bool bold = false)
            {
                totals.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                var f = bold ? fontB : fontR;
                var labelCell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetBorderTop(new SolidBorder(ColorBorder, 0.5f))
                    .SetTextAlignment(TextAlignment.RIGHT).SetPaddingRight(4);
                labelCell.Add(new Paragraph(label).SetFont(f).SetFontSize(9));
                totals.AddCell(labelCell);
                var valCell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetBorderTop(new SolidBorder(ColorBorder, 0.5f))
                    .SetTextAlignment(TextAlignment.RIGHT);
                valCell.Add(new Paragraph(val).SetFont(f).SetFontSize(9));
                totals.AddCell(valCell);
            }
            TotalRow("Total", po.Total.ToString("N2"));
            TotalRow($"Vat {po.VatPercent:0}%", po.VatAmount.ToString("N2"));
            TotalRow("Grand total", po.GrandTotal.ToString("N2"), true);
            doc.Add(totals);

            doc.Add(new Paragraph($"( {po.GrandTotalText} )").SetFont(fontR).SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(8));

            // ── Notes ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(po.Notes))
            {
                doc.Add(new Paragraph("NOTES:").SetFont(fontB).SetFontSize(9));
                foreach (var line in po.Notes.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                    doc.Add(new Paragraph($"  {line.Trim()}").SetFont(fontR).SetFontSize(8).SetMarginBottom(1));
            }

            // ── Signature Section ─────────────────────────────
            doc.Add(new Paragraph("\n"));
            var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 }))
                .UseAllAvailableWidth().SetMarginTop(12);

            void SigCell(string role, string name, string titleStr, string signBase64, string sigImgBase64)
            {
                var cell = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER).SetPaddingTop(4);
                cell.Add(new Paragraph(role).SetFont(fontB).SetFontSize(8).SetMarginBottom(6));

                // แสดงรูปลายเซ็น (ถ้ามี) หรือกรอบเปล่า
                if (!string.IsNullOrEmpty(sigImgBase64))
                {
                    try
                    {
                        var imgBytes = Convert.FromBase64String(sigImgBase64);
                        var imgData = iText.IO.Image.ImageDataFactory.Create(imgBytes);
                        var img = new Image(imgData).SetWidth(100).SetHeight(40);
                        cell.Add(img);
                    }
                    catch
                    {
                        cell.Add(new Paragraph("[ Signed ]").SetFont(fontR).SetFontSize(7).SetFontColor(ColorSignedGreen));
                    }
                }
                else if (!string.IsNullOrEmpty(signBase64))
                {
                    cell.Add(new Paragraph("✓ Digitally Signed").SetFont(fontR).SetFontSize(7).SetFontColor(ColorSignedGreen));
                    cell.Add(new Paragraph(" \n ").SetFont(fontR).SetFontSize(6));
                }
                else
                {
                    cell.Add(new Paragraph("\n\n").SetFont(fontR).SetFontSize(8));
                }

                cell.Add(new Paragraph(name).SetFont(fontB).SetFontSize(8));
                cell.Add(new Paragraph(titleStr).SetFont(fontR).SetFontSize(7));
                sigTable.AddCell(cell);
            }

            SigCell("For Supplier\nSign & Feedback", "", "", "", "");
            SigCell("Requested:", po.RequesterName, po.RequesterTitle,
                po.RequesterSignatureBase64, po.RequesterSignatureImage);
            SigCell("Issued:", po.IssuerName, po.IssuerTitle,
                po.IssuerSignatureBase64, po.IssuerSignatureImage);
            SigCell("Authorized:", !string.IsNullOrEmpty(po.Approver2Name) ? po.Approver2Name : po.Approver1Name ?? "",
                !string.IsNullOrEmpty(po.Approver2Title) ? po.Approver2Title : po.Approver1Title ?? "",
                !string.IsNullOrEmpty(po.Approver2SignatureBase64) ? po.Approver2SignatureBase64 : po.Approver1SignatureBase64 ?? "",
                !string.IsNullOrEmpty(po.Approver2SignatureImage) ? po.Approver2SignatureImage : po.Approver1SignatureImage ?? "");

            doc.Add(sigTable);
            // Footer image วาดโดย LetterheadPageEvent อัตโนมัติทุกหน้า

            doc.Close();

            // ── Two-pass: stamp "Page X of Y" ────────────────────
            return AddPageNumbers(ms.ToArray());
        }

        /// <summary>
        /// Second pass — stamp "Page X of Y" มุมซ้ายล่างทุกหน้า
        /// ทำหลัง doc.Close() เพราะต้องรู้ total pages ก่อน
        /// </summary>
        private static byte[] AddPageNumbers(byte[] pdfBytes)
        {
            using var msIn = new MemoryStream(pdfBytes);
            using var msOut = new MemoryStream();

            var reader = new PdfReader(msIn);
            var writer2 = new PdfWriter(msOut);
            var pdf2 = new PdfDocument(reader, writer2);
            var fontR = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            int total = pdf2.GetNumberOfPages();

            for (int i = 1; i <= total; i++)
            {
                var page = pdf2.GetPage(i);
                var canvas = new PdfCanvas(page.NewContentStreamAfter(),
                                           page.GetResources(), pdf2);
                var pageNum = $"Page {i} of {total}";
                var pageSize2 = pdf2.GetPage(i).GetPageSize();
                // คำนวณ x ให้ชิดขวา: pageWidth - margin - textWidth
                float textWidth = fontR.GetWidth(pageNum, 7f);
                float xRight = pageSize2.GetWidth() - 20f - textWidth;

                canvas.BeginText()
                      .SetFontAndSize(fontR, 7f)
                      .MoveText(xRight, 70f)
                      .ShowText(pageNum)
                      .EndText()
                      .Release();
            }

            pdf2.Close();
            return msOut.ToArray();
        }

        // ══════════════════════════════════════════════════════
        // PRINT MODE — Content only, no header/footer
        // สำหรับพิมพ์ลง Bernina Blank Form (letterhead กระดาษ)
        // Margin: บน 2.5cm ล่าง 2.5cm ซ้าย 1.5cm ขวา 1cm
        // ══════════════════════════════════════════════════════
        public async Task<byte[]> GeneratePOPdfForPrintAsync(PORequestModel po)
        {
            await Task.CompletedTask;

            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf, PageSize.A4);

            // Margins: top=2.5cm, bottom=2.5cm, left=1.5cm, right=1cm
            // 1 cm = 28.35 pt
            doc.SetMargins(70.9f, 28.35f, 70.9f, 42.5f);

            // ใช้ Tahoma (รองรับภาษาไทย) แทน Helvetica
            var fontR = LoadThaiFont(FontPathsRegular, StandardFonts.HELVETICA);
            var fontB = LoadThaiFont(FontPathsBold, StandardFonts.HELVETICA_BOLD);

            // ── PO Number + Date ──────────────────────────────
            var titleTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                .UseAllAvailableWidth().SetMarginBottom(6);

            var tL = new Cell().SetBorder(Border.NO_BORDER);
            tL.Add(new Paragraph("PURCHASE ORDER No.").SetFont(fontB).SetFontSize(9));
            tL.Add(new Paragraph(po.PONumber).SetFont(fontB).SetFontSize(14).SetFontColor(ColorHeader));
            titleTable.AddCell(tL);

            var tR = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            tR.Add(new Paragraph($"Date:  {po.PODate:M/d/yyyy}").SetFont(fontR).SetFontSize(9));
            if (!string.IsNullOrEmpty(po.InternalRefAC))
                tR.Add(new Paragraph($"For BT. Internal Ref.: A/C   {po.InternalRefAC}").SetFont(fontR).SetFontSize(9));
            if (!string.IsNullOrEmpty(po.CreditNo))
                tR.Add(new Paragraph($"Credit No:  {po.CreditNo}").SetFont(fontR).SetFontSize(9));
            titleTable.AddCell(tR);
            doc.Add(titleTable);

            // ── Vendor Info ───────────────────────────────────
            void LabelVal(string label, string val) =>
                doc.Add(new Paragraph().SetFont(fontR).SetFontSize(9).SetMarginBottom(2)
                    .Add(new Text(label + "  ").SetFont(fontB))
                    .Add(new Text(val)));

            LabelVal("Attn:", po.VendorAttn);
            LabelVal("Company:", po.VendorCompany);
            LabelVal("Address:", po.VendorAddress);

            if (!string.IsNullOrEmpty(po.VendorTel) || !string.IsNullOrEmpty(po.VendorFax) || !string.IsNullOrEmpty(po.VendorEmail))
            {
                // Tel / Fax / Email บรรทัดเดียวกัน
                var contactTable = new Table(UnitValue.CreatePercentArray(new float[] { 34, 33, 33 }))
                    .UseAllAvailableWidth().SetMarginBottom(2);

                var telCell = new Cell().SetBorder(Border.NO_BORDER);
                telCell.Add(new Paragraph().SetFont(fontR).SetFontSize(9)
                    .Add(new Text("Tel No.:  ").SetFont(fontB))
                    .Add(new Text(po.VendorTel ?? "")));
                contactTable.AddCell(telCell);

                var faxCell = new Cell().SetBorder(Border.NO_BORDER);
                faxCell.Add(new Paragraph().SetFont(fontR).SetFontSize(9)
                    .Add(new Text("Fax No.:  ").SetFont(fontB))
                    .Add(new Text(po.VendorFax ?? "")));
                contactTable.AddCell(faxCell);

                var emailCell = new Cell().SetBorder(Border.NO_BORDER);
                emailCell.Add(new Paragraph().SetFont(fontR).SetFontSize(9)
                    .Add(new Text("E-mail:  ").SetFont(fontB))
                    .Add(new Text(po.VendorEmail ?? "")));
                contactTable.AddCell(emailCell);

                doc.Add(contactTable);
            }
            if (!string.IsNullOrEmpty(po.RefNo)) LabelVal("Ref.:", po.RefNo);

            // ── Reference fields (optional) ──────────────────
            bool hasRef = !string.IsNullOrEmpty(po.InternalContact) || !string.IsNullOrEmpty(po.OldPONumber)
                       || !string.IsNullOrEmpty(po.WorkOrderNo)     || !string.IsNullOrEmpty(po.NCRNo)
                       || !string.IsNullOrEmpty(po.IRCRNo)          || !string.IsNullOrEmpty(po.ChangeNo);
            if (hasRef)
            {
                var refTbl = new Table(UnitValue.CreatePercentArray(new float[] { 33, 33, 34 }))
                    .UseAllAvailableWidth().SetMarginBottom(2);

                void RefCell(string label, string? val)
                {
                    var c = new Cell().SetBorder(Border.NO_BORDER);
                    if (!string.IsNullOrEmpty(val))
                        c.Add(new Paragraph().SetFont(fontR).SetFontSize(8.5f).SetMarginBottom(1)
                            .Add(new Text(label + "  ").SetFont(fontB))
                            .Add(new Text(val)));
                    else
                        c.Add(new Paragraph(" ").SetFont(fontR).SetFontSize(8.5f));
                    refTbl.AddCell(c);
                }

                RefCell("Contact:", po.InternalContact);
                RefCell("Ref. Old PO:", po.OldPONumber);
                RefCell("", null);   // spacer
                RefCell("WO No.:", po.WorkOrderNo);
                RefCell("NCR No.:", po.NCRNo);
                RefCell("IR/CR No.:", po.IRCRNo);
                if (!string.IsNullOrEmpty(po.ChangeNo))
                {
                    var cc = new Cell(1, 3).SetBorder(Border.NO_BORDER);
                    cc.Add(new Paragraph().SetFont(fontR).SetFontSize(8.5f).SetMarginBottom(1)
                        .Add(new Text("Change No.:  ").SetFont(fontB))
                        .Add(new Text(po.ChangeNo)));
                    refTbl.AddCell(cc);
                }

                doc.Add(refTbl);
            }

            doc.Add(new Paragraph($"Subject:  {po.Subject}").SetFont(fontR).SetFontSize(9).SetMarginBottom(8));

            // ── Line Items ────────────────────────────────────
            var tbl = new Table(UnitValue.CreatePercentArray(new float[] { 5, 52, 14, 15, 14 }))
                .UseAllAvailableWidth().SetMarginBottom(6);

            void AddHeader(string h) =>
                tbl.AddHeaderCell(new Cell().SetBackgroundColor(ColorHeader).SetBorder(Border.NO_BORDER)
                    .Add(new Paragraph(h).SetFont(fontB).SetFontSize(8)
                        .SetFontColor(ColorHeaderText).SetTextAlignment(TextAlignment.CENTER)));
            AddHeader("#"); AddHeader("Description"); AddHeader("Quantity");
            AddHeader("Unit Price"); AddHeader("Amount/Baht");

            bool alt = false;
            foreach (var item in po.LineItems)
            {
                var bg = alt ? ColorRowAlt : null;
                void AddCell(string v, bool right = false)
                {
                    var c = new Cell().SetBorder(Border.NO_BORDER)
                        .SetBorderBottom(new SolidBorder(ColorBorder, 0.5f));
                    if (bg != null) c.SetBackgroundColor(bg);
                    c.Add(new Paragraph(v).SetFont(fontR).SetFontSize(9)
                        .SetTextAlignment(right ? TextAlignment.RIGHT : TextAlignment.LEFT));
                    tbl.AddCell(c);
                }
                AddCell(item.LineNo.ToString());
                AddCell(item.Description);
                AddCell(item.Quantity.ToString("N3"), true);
                AddCell(item.UnitPrice.ToString("N2"), true);
                AddCell(item.Amount.ToString("N2"), true);
                alt = !alt;
            }
            doc.Add(tbl);

            // ── Totals — จัดให้ตรงกับ column Amount (14%) ────────
            var totals = new Table(UnitValue.CreatePercentArray(new float[] { 71, 15, 14 }))
                .UseAllAvailableWidth().SetMarginBottom(4);
            void TotalRow(string label, string val, bool bold = false)
            {
                totals.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                var f = bold ? fontB : fontR;
                var labelCell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetBorderTop(new SolidBorder(ColorBorder, 0.5f))
                    .SetTextAlignment(TextAlignment.RIGHT).SetPaddingRight(4);
                labelCell.Add(new Paragraph(label).SetFont(f).SetFontSize(9));
                totals.AddCell(labelCell);
                var valCell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetBorderTop(new SolidBorder(ColorBorder, 0.5f))
                    .SetTextAlignment(TextAlignment.RIGHT);
                valCell.Add(new Paragraph(val).SetFont(f).SetFontSize(9));
                totals.AddCell(valCell);
            }
            TotalRow("Total", po.Total.ToString("N2"));
            TotalRow($"Vat {po.VatPercent:0}%", po.VatAmount.ToString("N2"));
            TotalRow("Grand total", po.GrandTotal.ToString("N2"), true);
            doc.Add(totals);

            doc.Add(new Paragraph($"( {po.GrandTotalText} )")
                .SetFont(fontR).SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(8));

            // ── Notes ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(po.Notes))
            {
                doc.Add(new Paragraph("NOTES:").SetFont(fontB).SetFontSize(9));
                foreach (var line in po.Notes.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                    doc.Add(new Paragraph($"  {line.Trim()}").SetFont(fontR).SetFontSize(8).SetMarginBottom(1));
            }

            // ── Signature Section ─────────────────────────────
            doc.Add(new Paragraph("\n"));
            var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 }))
                .UseAllAvailableWidth().SetMarginTop(10);

            void SigCellPrint(string role, string name, string titleStr, string sigImgBase64)
            {
                var cell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER).SetPaddingTop(4);

                cell.Add(new Paragraph(role).SetFont(fontB).SetFontSize(8).SetMarginBottom(4));

                if (!string.IsNullOrEmpty(sigImgBase64))
                {
                    try
                    {
                        var imgBytes = Convert.FromBase64String(sigImgBase64);
                        var imgData = iText.IO.Image.ImageDataFactory.Create(imgBytes);
                        var img = new Image(imgData)
                            .SetWidth(90).SetHeight(36)
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER);
                        cell.Add(img);
                    }
                    catch
                    {
                        cell.Add(new Paragraph("[ Signed ]").SetFont(fontR).SetFontSize(7)
                            .SetFontColor(ColorSignedGreen));
                    }
                }
                else
                {
                    // กรอบว่างสำหรับลงนาม
                    cell.Add(new Paragraph("\n\n").SetFont(fontR).SetFontSize(9));
                }

                cell.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.DottedLine())
                    .SetMarginTop(2).SetMarginBottom(3));
                if (!string.IsNullOrEmpty(name))
                {
                    cell.Add(new Paragraph(name).SetFont(fontB).SetFontSize(8));
                    if (!string.IsNullOrEmpty(titleStr))
                        cell.Add(new Paragraph(titleStr).SetFont(fontR).SetFontSize(7)
                            .SetFontColor(new DeviceRgb(0x55, 0x55, 0x55)));
                }
                sigTable.AddCell(cell);
            }

            SigCellPrint("For Supplier\nSign & Feedback", "", "", "");
            SigCellPrint("Requested:", po.RequesterName, po.RequesterTitle, po.RequesterSignatureImage);
            SigCellPrint("Issued:", po.IssuerName, po.IssuerTitle, po.IssuerSignatureImage);
            SigCellPrint("Authorized:", !string.IsNullOrEmpty(po.Approver2Name) ? po.Approver2Name : po.Approver1Name ?? "",
                !string.IsNullOrEmpty(po.Approver2Title) ? po.Approver2Title : po.Approver1Title ?? "",
                !string.IsNullOrEmpty(po.Approver2SignatureImage) ? po.Approver2SignatureImage : po.Approver1SignatureImage ?? "");

            doc.Add(sigTable);
            doc.Close();
            return ms.ToArray();
        }
    }
}

// ── Page Event: วาด letterhead header + footer ทุกหน้า ──
// Header: BERNINA logo (top-left)
// Footer: company info text (left) + Swiss Heritage logo (right)
public class LetterheadPageEvent : IEventHandler
{
    private readonly string _logoPath;
    private readonly string _swissLogoPath;

    private static readonly string[] ThaiRegularPaths = {
        @"C:\Windows\Fonts\tahoma.ttf",
        @"C:\Windows\Fonts\arial.ttf",
    };
    private static readonly string[] ThaiBoldPaths = {
        @"C:\Windows\Fonts\tahomabd.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
    };

    public LetterheadPageEvent(string logoPath, string swissLogoPath)
    {
        _logoPath = logoPath;
        _swissLogoPath = swissLogoPath;
    }

    private static PdfFont LoadFont(string[] paths)
    {
        foreach (var p in paths)
            if (System.IO.File.Exists(p))
                return PdfFontFactory.CreateFont(p, PdfEncodings.IDENTITY_H,
                       PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
        return PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
    }

    public void HandleEvent(Event @event)
    {
        if (@event is not PdfDocumentEvent docEvent) return;

        var page = docEvent.GetPage();
        var pdfDoc = docEvent.GetDocument();
        var canvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdfDoc);
        var pageSize = page.GetPageSize();
        float pageW = pageSize.GetWidth();   // A4 = 595 pt
        float pageH = pageSize.GetHeight();  // A4 = 842 pt

        // ── Header: BERNINA logo (top-left) ──────────────────────
        if (System.IO.File.Exists(_logoPath))
        {
            try
            {
                var imgData = ImageDataFactory.Create(_logoPath);
                // Aspect ratio ของรูปจริง: 246×68 px → ratio 3.618
                // กำหนด height แล้วคำนวณ width เพื่อให้ถูกสัดส่วน
                float logoH = 45f;
                float logoW = logoH * (246f / 68f);  // = 163pt
                float logoY = pageH - logoH - 8f;   // ~786 pt from bottom
                canvas.AddImageFittedIntoRectangle(imgData,
                    new Rectangle(20f, logoY, logoW, logoH), false);
            }
            catch { }
        }

        // ── Footer: thin rule ─────────────────────────────────────
        canvas.SaveState()
              .SetLineWidth(0.5f)
              .SetStrokeColor(new DeviceRgb(0xCC, 0xCC, 0xCC))
              .MoveTo(20f, 66f)
              .LineTo(pageW - 20f, 66f)
              .Stroke()
              .RestoreState();

        // ── Footer: Swiss Heritage logo (right) ──────────────────
        float swissSize = 46f;
        float swissX = pageW - swissSize - 18f;
        if (System.IO.File.Exists(_swissLogoPath))
        {
            try
            {
                var swissData = ImageDataFactory.Create(_swissLogoPath);
                canvas.AddImageFittedIntoRectangle(swissData,
                    new Rectangle(swissX, 10f, swissSize, swissSize), false);
            }
            catch { }
        }

        // ── Footer: company info text (left) ─────────────────────
        try
        {
            var fontR = LoadFont(ThaiRegularPaths);
            var fontB = LoadFont(ThaiBoldPaths);
            var darkGray = new DeviceRgb(0x33, 0x33, 0x33);
            var black    = new DeviceRgb(0x00, 0x00, 0x00);

            // "BERNINA (Thailand) Co.,Ltd" — bold
            canvas.BeginText()
                  .SetFontAndSize(fontB, 8f)
                  .SetFillColor(black)
                  .MoveText(20f, 53f)
                  .ShowText("BERNINA (Thailand) Co.,Ltd")
                  .EndText();

            // English address + phone
            canvas.BeginText()
                  .SetFontAndSize(fontR, 6.5f)
                  .SetFillColor(darkGray)
                  .MoveText(20f, 44f)
                  .ShowText("79/1 Moo 4 Banklang Muang Lamphun 51000 Thailand  T +66 (0)53 581 343 - 9")
                  .EndText();

            // Thai address
            canvas.BeginText()
                  .SetFontAndSize(fontR, 6.5f)
                  .SetFillColor(darkGray)
                  .MoveText(20f, 35f)
                  .ShowText("บริษัท เบอร์นิน่า (ประเทศไทย) จำกัด  เลขที่ 79/1  หมู่ 4  ต.บ้านกลาง  อ.เมือง  จ.ลำพูน  51000")
                  .EndText();

            // Email / website
            canvas.BeginText()
                  .SetFontAndSize(fontR, 6.5f)
                  .SetFillColor(darkGray)
                  .MoveText(20f, 26f)
                  .ShowText("info@berninathailand.com; bernina.com")
                  .EndText();
        }
        catch { }

        canvas.Release();
    }
}
