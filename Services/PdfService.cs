using BTITPORequest.Models;
using BTITPORequest.Services.Interfaces;
using iText.IO.Font.Constants;
using iText.IO.Image;
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
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfService> _logger;

        // Color palette
        private static readonly DeviceRgb ColorHeader = new(0x1A, 0x56, 0x76);  // Dark teal
        private static readonly DeviceRgb ColorHeaderText = new(0xFF, 0xFF, 0xFF);
        private static readonly DeviceRgb ColorRowAlt = new(0xF0, 0xF7, 0xFB);
        private static readonly DeviceRgb ColorBorderLight = new(0xCC, 0xCC, 0xCC);

        public PdfService(IConfiguration config, IWebHostEnvironment env, ILogger<PdfService> logger)
        {
            _config = config;
            _env = env;
            _logger = logger;
        }

        public async Task<byte[]> GeneratePOPdfAsync(PORequestModel po)
        {
            await Task.CompletedTask; // Make async-compatible

            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf, PageSize.A4);
            doc.SetMargins(30, 30, 30, 30);

            var fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            var appSettings = _config.GetSection("AppSettings");
            var companyName = appSettings["CompanyName"] ?? "BERNINA Thailand";
            var tradeRegNo = appSettings["CompanyTradeRegisterNo"] ?? "";
            var vatNo = appSettings["CompanyVatNo"] ?? "";
            var address = appSettings["CompanyAddress"] ?? "";
            var tel = appSettings["CompanyTel"] ?? "";

            // ─── HEADER ───────────────────────────────────────────
            var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 }))
                .UseAllAvailableWidth();

            // Left: Trade info
            var leftHeader = new Cell().SetBorder(Border.NO_BORDER);
            leftHeader.Add(new Paragraph($"TradeRegister No. {tradeRegNo}")
                .SetFont(fontRegular).SetFontSize(8));
            leftHeader.Add(new Paragraph($"Input Value Added Tax No. {vatNo}")
                .SetFont(fontRegular).SetFontSize(8));
            headerTable.AddCell(leftHeader);

            // Right: Brand
            var rightHeader = new Cell().SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.RIGHT);
            rightHeader.Add(new Paragraph("made to create  BERNINA")
                .SetFont(fontBold).SetFontSize(18).SetFontColor(ColorHeader));
            headerTable.AddCell(rightHeader);

            doc.Add(headerTable);
            doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f))
                .SetMarginBottom(8));

            // ─── PO TITLE ROW ─────────────────────────────────────
            var titleTable = new Table(UnitValue.CreatePercentArray(new float[] { 40, 60 }))
                .UseAllAvailableWidth().SetMarginBottom(6);

            var titleLeft = new Cell().SetBorder(Border.NO_BORDER);
            titleLeft.Add(new Paragraph("PURCHASE ORDER No. :")
                .SetFont(fontBold).SetFontSize(11));
            titleLeft.Add(new Paragraph(po.PONumber)
                .SetFont(fontBold).SetFontSize(14).SetFontColor(ColorHeader));
            titleTable.AddCell(titleLeft);

            var titleRight = new Cell().SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.RIGHT);
            titleRight.Add(new Paragraph($"Date:  {po.PODate:M/d/yyyy}")
                .SetFont(fontRegular).SetFontSize(9));
            titleRight.Add(new Paragraph($"For BT. Internal Ref.: A/C   {po.InternalRefAC}")
                .SetFont(fontRegular).SetFontSize(9));
            if (!string.IsNullOrEmpty(po.CreditNo))
                titleRight.Add(new Paragraph($"Credit No:  {po.CreditNo}")
                    .SetFont(fontRegular).SetFontSize(9));
            titleTable.AddCell(titleRight);

            doc.Add(titleTable);

            // ─── VENDOR INFO ──────────────────────────────────────
            AddLabelValue(doc, fontRegular, fontBold, "Attn:", po.VendorAttn);
            AddLabelValue(doc, fontRegular, fontBold, "Company:", po.VendorCompany);
            AddLabelValue(doc, fontRegular, fontBold, "Address:", po.VendorAddress);

            var contactTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                .UseAllAvailableWidth().SetMarginBottom(4);
            var telCell = new Cell().SetBorder(Border.NO_BORDER);
            telCell.Add(new Paragraph().SetFont(fontRegular).SetFontSize(9)
                .Add(new Text("Tel No.: ").SetFont(fontBold))
                .Add(new Text(po.VendorTel)));
            var faxCell = new Cell().SetBorder(Border.NO_BORDER);
            faxCell.Add(new Paragraph().SetFont(fontRegular).SetFontSize(9)
                .Add(new Text("Fax No.: ").SetFont(fontBold))
                .Add(new Text(po.VendorFax)));
            contactTable.AddCell(telCell);
            contactTable.AddCell(faxCell);
            doc.Add(contactTable);

            AddLabelValue(doc, fontRegular, fontBold, "E-mail:", po.VendorEmail);
            AddLabelValue(doc, fontRegular, fontBold, "Ref.:", po.RefNo);

            doc.Add(new Paragraph($"Subject:  please supply the following goods / services to us:")
                .SetFont(fontRegular).SetFontSize(9).SetMarginBottom(8));

            // ─── LINE ITEMS TABLE ─────────────────────────────────
            var itemTable = new Table(UnitValue.CreatePercentArray(new float[] { 5, 50, 15, 15, 15 }))
                .UseAllAvailableWidth().SetMarginBottom(8);

            AddTableHeader(itemTable, fontBold, "#", "Description", "Quantity", "Unit Price", "Amount/Baht");

            bool alt = false;
            foreach (var item in po.LineItems)
            {
                var bg = alt ? ColorRowAlt : null;
                AddTableRow(itemTable, fontRegular, bg,
                    item.LineNo.ToString(),
                    item.Description,
                    item.Quantity.ToString("N3"),
                    item.UnitPrice.ToString("N2"),
                    item.Amount.ToString("N2"));
                alt = !alt;
            }

            doc.Add(itemTable);

            // ─── TOTALS ───────────────────────────────────────────
            var totalsTable = new Table(UnitValue.CreatePercentArray(new float[] { 70, 30 }))
                .UseAllAvailableWidth().SetMarginBottom(4);
            totalsTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
            totalsTable.AddCell(CreateTotalRow(fontRegular, fontBold, "Total", po.Total.ToString("N2")));
            totalsTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
            totalsTable.AddCell(CreateTotalRow(fontRegular, fontBold, $"Vat {po.VatPercent:0}%", po.VatAmount.ToString("N2")));
            totalsTable.AddCell(new Cell().SetBorder(Border.NO_BORDER));
            totalsTable.AddCell(CreateTotalRow(fontRegular, fontBold, "Grand total", po.GrandTotal.ToString("N2"), isBold: true));
            doc.Add(totalsTable);

            // Amount in words
            doc.Add(new Paragraph($"( {po.GrandTotalText} )")
                .SetFont(fontRegular).SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(8));

            // ─── NOTES ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(po.Notes))
            {
                doc.Add(new Paragraph("NOTES:").SetFont(fontBold).SetFontSize(9));
                foreach (var note in po.Notes.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(note))
                        doc.Add(new Paragraph($"  {note.Trim()}").SetFont(fontRegular).SetFontSize(8).SetMarginBottom(1));
                }
            }

            // ─── SIGNATURE SECTION ────────────────────────────────
            doc.Add(new Paragraph("\n"));
            var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 }))
                .UseAllAvailableWidth().SetMarginTop(16);

            AddSignatureCell(sigTable, fontRegular, fontBold, "For Supplier Sign & Feedback", "", "", null);
            AddSignatureCell(sigTable, fontRegular, fontBold, "Requested:", po.RequesterName, po.RequesterTitle, po.RequesterSignUrl);
            AddSignatureCell(sigTable, fontRegular, fontBold, "Issued:", po.IssuerName, po.IssuerTitle, po.IssuerSignUrl);
            AddSignatureCell(sigTable, fontRegular, fontBold, "Authorized:", po.Approver1Name ?? po.Approver2Name, po.Approver1Title ?? po.Approver2Title, po.Approver2SignUrl ?? po.Approver1SignUrl);

            doc.Add(sigTable);

            // ─── FOOTER ───────────────────────────────────────────
            doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f)).SetMarginTop(16));
            doc.Add(new Paragraph($"{companyName}  |  {address}  |  T {tel}")
                .SetFont(fontRegular).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(new DeviceRgb(0x66, 0x66, 0x66)));

            doc.Close();
            return ms.ToArray();
        }

        private static void AddLabelValue(Document doc, PdfFont fontRegular, PdfFont fontBold, string label, string value)
        {
            doc.Add(new Paragraph()
                .SetFont(fontRegular).SetFontSize(9).SetMarginBottom(2)
                .Add(new Text(label + "  ").SetFont(fontBold))
                .Add(new Text(value)));
        }

        private static void AddTableHeader(Table table, PdfFont fontBold, params string[] headers)
        {
            foreach (var h in headers)
            {
                table.AddHeaderCell(new Cell()
                    .SetBackgroundColor(ColorHeader)
                    .SetBorder(Border.NO_BORDER)
                    .Add(new Paragraph(h)
                        .SetFont(fontBold).SetFontSize(9)
                        .SetFontColor(ColorHeaderText)
                        .SetTextAlignment(TextAlignment.CENTER)));
            }
        }

        private static void AddTableRow(Table table, PdfFont font, DeviceRgb? bg, params string[] values)
        {
            bool first = true;
            foreach (var v in values)
            {
                var cell = new Cell().SetBorder(Border.NO_BORDER)
                    .SetBorderBottom(new SolidBorder(ColorBorderLight, 0.5f))
                    .Add(new Paragraph(v).SetFont(font).SetFontSize(9));
                if (bg != null) cell.SetBackgroundColor(bg);
                if (!first) cell.SetTextAlignment(TextAlignment.RIGHT);
                table.AddCell(cell);
                first = false;
            }
        }

        private static Cell CreateTotalRow(PdfFont font, PdfFont fontBold, string label, string value, bool isBold = false)
        {
            var f = isBold ? fontBold : font;
            var cell = new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetBorderTop(new SolidBorder(ColorBorderLight, 0.5f));
            var p = new Paragraph()
                .SetFont(f).SetFontSize(9)
                .Add(new Text(label + "  ").SetFont(font))
                .Add(new Text(value).SetFont(fontBold));
            cell.Add(p);
            return cell;
        }

        private static void AddSignatureCell(Table table, PdfFont font, PdfFont fontBold, string role, string name, string title, string? signUrl)
        {
            var cell = new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPaddingTop(4);

            cell.Add(new Paragraph(role).SetFont(fontBold).SetFontSize(8).SetMarginBottom(4));

            // Signature image placeholder
            if (!string.IsNullOrEmpty(signUrl))
            {
                cell.Add(new Paragraph("[Digital Signature Applied]")
                    .SetFont(font).SetFontSize(7)
                    .SetFontColor(new DeviceRgb(0x28, 0x7A, 0x4C)));
            }
            else
            {
                cell.Add(new Paragraph(" \n\n")
                    .SetFont(font).SetFontSize(8));
            }

            if (!string.IsNullOrEmpty(name))
            {
                cell.Add(new Paragraph(name).SetFont(fontBold).SetFontSize(8));
                cell.Add(new Paragraph(title).SetFont(font).SetFontSize(7));
            }

            table.AddCell(cell);
        }
    }
}
