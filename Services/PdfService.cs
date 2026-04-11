using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
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
            doc.SetMargins(30, 30, 30, 30);

            var fontR = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var fontB = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // ── Header ────────────────────────────────────────
            var hdr = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 })).UseAllAvailableWidth();
            var hdrL = new Cell().SetBorder(Border.NO_BORDER);
            hdrL.Add(new Paragraph($"TradeRegister No. {appSettings["CompanyTradeRegisterNo"]}").SetFont(fontR).SetFontSize(8));
            hdrL.Add(new Paragraph($"Input Value Added Tax No. {appSettings["CompanyVatNo"]}").SetFont(fontR).SetFontSize(8));
            hdr.AddCell(hdrL);
            var hdrR = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            hdrR.Add(new Paragraph($"made to create  {appSettings["CompanyName"]}").SetFont(fontB).SetFontSize(16).SetFontColor(ColorHeader));
            hdr.AddCell(hdrR);
            doc.Add(hdr);
            doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f)).SetMarginBottom(6));

            // ── PO Title ──────────────────────────────────────
            var title = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth().SetMarginBottom(6);
            var tL = new Cell().SetBorder(Border.NO_BORDER);
            tL.Add(new Paragraph("PURCHASE ORDER No.").SetFont(fontB).SetFontSize(10));
            tL.Add(new Paragraph(po.PONumber).SetFont(fontB).SetFontSize(14).SetFontColor(ColorHeader));
            title.AddCell(tL);
            var tR = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            tR.Add(new Paragraph($"Date:  {po.PODate:M/d/yyyy}").SetFont(fontR).SetFontSize(9));
            tR.Add(new Paragraph($"For BT. Internal Ref.: A/C   {po.InternalRefAC}").SetFont(fontR).SetFontSize(9));
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

            // ── Totals ────────────────────────────────────────
            var totals = new Table(UnitValue.CreatePercentArray(new float[] { 70, 30 })).UseAllAvailableWidth().SetMarginBottom(4);
            void TotalRow(string label, string val, bool bold = false)
            {
                totals.AddCell(new Cell().SetBorder(Border.NO_BORDER));
                var f = bold ? fontB : fontR;
                var c = new Cell().SetBorder(Border.NO_BORDER).SetBorderTop(new SolidBorder(ColorBorder, 0.5f));
                c.Add(new Paragraph().SetFont(fontR).SetFontSize(9)
                    .Add(new Text(label + "  "))
                    .Add(new Text(val).SetFont(f)));
                totals.AddCell(c);
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
            SigCell("Authorized:", po.Approver2Name ?? po.Approver1Name ?? "",
                po.Approver2Title ?? po.Approver1Title ?? "",
                po.Approver2SignatureBase64 ?? po.Approver1SignatureBase64 ?? "",
                po.Approver2SignatureImage ?? po.Approver1SignatureImage ?? "");

            doc.Add(sigTable);

            // ── Footer ────────────────────────────────────────
            doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f)).SetMarginTop(16));
            doc.Add(new Paragraph($"{appSettings["CompanyName"]}  |  {appSettings["CompanyAddress"]}  |  T {appSettings["CompanyTel"]}")
                .SetFont(fontR).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(new DeviceRgb(0x66, 0x66, 0x66)));

            doc.Close();
            return ms.ToArray();
        }
    }
}
