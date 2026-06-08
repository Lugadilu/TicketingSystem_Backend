using System;
using System.IO;
using System.Threading.Tasks;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Dtos.Tickets;
using TicketingSystem.Api.Models;

namespace TicketingSystem.Api.Services;

public interface IPdfService
{
    /// <summary>
    /// Generate a PDF document for a ticket with full details, comments, and activity logs
    /// </summary>
    Task<byte[]> GenerateTicketPdfAsync(Guid ticketId);

    /// <summary>
    /// Generate a PDF document for a technician's resolution report
    /// </summary>
    Task<byte[]> GenerateReportPdfAsync(Guid reportId);

    /// <summary>
    /// Generate a combined PDF with both ticket details and the technician report
    /// </summary>
    Task<byte[]> GenerateTicketWithReportPdfAsync(Guid ticketId);
}

public class PdfService : IPdfService
{
    private readonly AppDbContext _context;

    public PdfService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateTicketPdfAsync(Guid ticketId)
    {
        var ticket = await LoadTicketForPdfAsync(ticketId);
        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        using var ms = new MemoryStream();
        var doc = new Document(PageSize.A4, 36, 36, 72, 36);
        var writer = PdfWriter.GetInstance(doc, ms);
        doc.Open();

        // Header
        AddHeader(doc, "TICKET DETAILS");
        AddTicketInfo(doc, ticket);
        AddCommentsSection(doc, ticket.Comments);
        AddActivityLogSection(doc, ticket.ActivityLogs);

        doc.Close();
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateReportPdfAsync(Guid reportId)
    {
        var report = await _context.TicketReports
            .Include(r => r.Ticket)
                .ThenInclude(t => t.CreatedByUser)
            .Include(r => r.TechnicianUser)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
            throw new InvalidOperationException("Report not found");

        using var ms = new MemoryStream();
        var doc = new Document(PageSize.A4, 36, 36, 72, 36);
        var writer = PdfWriter.GetInstance(doc, ms);
        doc.Open();

        AddHeader(doc, "TECHNICIAN RESOLUTION REPORT");
        AddTicketSummary(doc, report.Ticket);
        AddReportDetails(doc, report);

        doc.Close();
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateTicketWithReportPdfAsync(Guid ticketId)
    {
        var ticket = await LoadTicketForPdfAsync(ticketId);
        if (ticket == null)
            throw new InvalidOperationException("Ticket not found");

        var report = await _context.TicketReports
            .Include(r => r.TechnicianUser)
            .FirstOrDefaultAsync(r => r.TicketId == ticketId);

        using var ms = new MemoryStream();
        var doc = new Document(PageSize.A4, 36, 36, 72, 36);
        var writer = PdfWriter.GetInstance(doc, ms);
        doc.Open();

        // Part 1: Ticket Details
        AddHeader(doc, "TICKET DETAILS");
        AddTicketInfo(doc, ticket);

        if (report != null)
        {
            doc.NewPage();
            AddHeader(doc, "TECHNICIAN RESOLUTION REPORT");
            AddReportDetails(doc, report);
        }

        doc.NewPage();
        AddCommentsSection(doc, ticket.Comments);

        doc.NewPage();
        AddActivityLogSection(doc, ticket.ActivityLogs);

        doc.Close();
        return ms.ToArray();
    }









    // ==================== HELPER METHODS ====================

    private async Task<Tickets?> LoadTicketForPdfAsync(Guid ticketId)
    {
        return await _context.Tickets
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ClosedByUser)
            .Include(t => t.Comments)
                .ThenInclude(c => c.User)
            .Include(t => t.ActivityLogs)
                .ThenInclude(a => a.ChangedByUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    private void AddHeader(Document doc, string title)
    {
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(41, 128, 185));
        var titlePara = new Paragraph(title, headerFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 20
        };
        doc.Add(titlePara);

        var line = new Paragraph(new Chunk(new LineSeparator(1f, 100f, BaseColor.LIGHT_GRAY, Element.ALIGN_CENTER, -1)))
        {
            SpacingAfter = 15
        };
        doc.Add(line);
    }

    private void AddTicketInfo(Document doc, Tickets ticket)
    {
        var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
        var cellBg = new BaseColor(41, 128, 185);

        var table = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 20 };
        table.SetWidths(new float[] { 1f, 2f });

        AddTableRow(table, "Ticket Number:", ticket.TicketNumber, cellBg, sectionFont);
        AddTableRow(table, "Title:", ticket.Title, cellBg, sectionFont);
        AddTableRow(table, "Description:", ticket.Description, cellBg, sectionFont);
        AddTableRow(table, "Category:", ticket.Category, cellBg, sectionFont);
        AddTableRow(table, "Status:", ticket.Status.ToString(), cellBg, sectionFont);
        AddTableRow(table, "Priority:", ((Priority)ticket.Priority).ToString(), cellBg, sectionFont);
        AddTableRow(table, "Created By:", $"{ticket.CreatedByUser.FirstName} {ticket.CreatedByUser.LastName}", cellBg, sectionFont);
        AddTableRow(table, "Assigned To:", ticket.AssignedToUser != null ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}" : "Unassigned", cellBg, sectionFont);
        AddTableRow(table, "Created At:", ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm"), cellBg, sectionFont);
        AddTableRow(table, "Updated At:", ticket.UpdatedAt.ToString("yyyy-MM-dd HH:mm"), cellBg, sectionFont);

        if (ticket.ClosedByUser != null)
        {
            AddTableRow(table, "Closed By:", $"{ticket.ClosedByUser.FirstName} {ticket.ClosedByUser.LastName}", cellBg, sectionFont);
            AddTableRow(table, "Closed At:", ticket.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A", cellBg, sectionFont);
        }

        doc.Add(table);
    }

    private void AddTicketSummary(Document doc, Tickets ticket)
    {
        var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
        var cellBg = new BaseColor(41, 128, 185);

        var table = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 20 };
        table.SetWidths(new float[] { 1f, 2f });

        AddTableRow(table, "Ticket Number:", ticket.TicketNumber, cellBg, sectionFont);
        AddTableRow(table, "Title:", ticket.Title, cellBg, sectionFont);
        AddTableRow(table, "Category:", ticket.Category, cellBg, sectionFont);
        AddTableRow(table, "Priority:", ((Priority)ticket.Priority).ToString(), cellBg, sectionFont);
        AddTableRow(table, "Customer:", $"{ticket.CreatedByUser.FirstName} {ticket.CreatedByUser.LastName}", cellBg, sectionFont);
        AddTableRow(table, "Created At:", ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm"), cellBg, sectionFont);

        doc.Add(table);
    }

    private void AddReportDetails(Document doc, TicketReport report)
    {
        var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
        var cellBg = new BaseColor(46, 204, 113);

        var table = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 20 };
        table.SetWidths(new float[] { 1f, 2f });

        AddTableRow(table, "Technician:", $"{report.TechnicianUser.FirstName} {report.TechnicianUser.LastName}", cellBg, sectionFont);
        AddTableRow(table, "Submitted At:", report.SubmittedAt.ToString("yyyy-MM-dd HH:mm"), cellBg, sectionFont);
        AddTableRow(table, "Time Spent:", report.TimeSpent, cellBg, sectionFont);
        AddTableRow(table, "Resolution Status:", report.IsResolved ? "Fully Resolved" : "Requires Follow-up", cellBg, sectionFont);

        doc.Add(table);

        // Free text sections
        AddSection(doc, "WORK SUMMARY", report.WorkSummary);
        AddSection(doc, "RESOLUTION STEPS", report.ResolutionSteps);

        if (!string.IsNullOrWhiteSpace(report.PartsUsed))
            AddSection(doc, "PARTS / TOOLS USED", report.PartsUsed);

        if (!string.IsNullOrWhiteSpace(report.Recommendations))
            AddSection(doc, "RECOMMENDATIONS", report.Recommendations);

        if (!report.IsResolved && !string.IsNullOrWhiteSpace(report.FollowUpNotes))
            AddSection(doc, "FOLLOW-UP NOTES", report.FollowUpNotes);
    }

    private void AddCommentsSection(Document doc, ICollection<TicketComment> comments)
    {
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(41, 128, 185));
        doc.Add(new Paragraph("COMMENTS", headerFont) { SpacingAfter = 10 });

        if (comments == null || comments.Count == 0)
        {
            doc.Add(new Paragraph("No comments on this ticket.", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
            return;
        }

        foreach (var comment in comments.OrderByDescending(c => c.CreatedAt))
        {
            var userName = comment.User != null ? $"{comment.User.FirstName} {comment.User.LastName}" : "Unknown";
            var metaFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(100, 100, 100));
            var meta = new Paragraph($"{userName}  |  {comment.CreatedAt:yyyy-MM-dd HH:mm}", metaFont)
            {
                SpacingAfter = 4
            };
            doc.Add(meta);

            var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var body = new Paragraph(comment.Comment, bodyFont)
            {
                SpacingAfter = 12
            };
            doc.Add(body);
        }
    }

    private void AddActivityLogSection(Document doc, ICollection<TicketActivityLog> logs)
    {
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(41, 128, 185));
        doc.Add(new Paragraph("ACTIVITY LOG", headerFont) { SpacingAfter = 10 });

        if (logs == null || logs.Count == 0)
        {
            doc.Add(new Paragraph("No activity recorded.", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
            return;
        }

        var table = new PdfPTable(4) { WidthPercentage = 100, SpacingAfter = 20 };
        table.SetWidths(new float[] { 1.5f, 1f, 1f, 1.5f });

        // Header row
        // var headerColor = new BaseColor(52, 73, 94);
        // var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
        // table.AddCell(CreateHeaderCell("Action", headerColor, headerFont));
        // table.AddCell(CreateHeaderCell("Old Value", headerColor, headerFont));
        // table.AddCell(CreateHeaderCell("New Value", headerColor, headerFont));
        // table.AddCell(CreateHeaderCell("Date", headerColor, headerFont));
        // Header row
        var headerColor = new BaseColor(52, 73, 94);
        var tableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);

        table.AddCell(CreateHeaderCell("Action", headerColor, tableHeaderFont));
        table.AddCell(CreateHeaderCell("Old Value", headerColor, tableHeaderFont));
        table.AddCell(CreateHeaderCell("New Value", headerColor, tableHeaderFont));
        table.AddCell(CreateHeaderCell("Date", headerColor, tableHeaderFont));

        // Data rows
        var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
        foreach (var log in logs.OrderByDescending(l => l.ChangedAt))
        {
            table.AddCell(CreateCell(log.Action, cellFont));
            table.AddCell(CreateCell(log.OldValue ?? "-", cellFont));
            table.AddCell(CreateCell(log.NewValue ?? "-", cellFont));
            table.AddCell(CreateCell(log.ChangedAt.ToString("yyyy-MM-dd HH:mm"), cellFont));
        }

        doc.Add(table);
    }

    private void AddSection(Document doc, string title, string content)
    {
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(46, 204, 113));
        doc.Add(new Paragraph(title, titleFont) { SpacingBefore = 10, SpacingAfter = 5 });

        var contentFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        doc.Add(new Paragraph(content, contentFont) { SpacingAfter = 10 });
    }

    private void AddTableRow(PdfPTable table, string label, string value, BaseColor labelBg, Font labelFont)
    {
        var labelCell = new PdfPCell(new Phrase(label, labelFont))
        {
            BackgroundColor = labelBg,
            Padding = 6,
            BorderColor = BaseColor.WHITE
        };

        var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        var valueCell = new PdfPCell(new Phrase(value ?? "N/A", valueFont))
        {
            Padding = 6,
            BorderColor = new BaseColor(220, 220, 220)
        };

        table.AddCell(labelCell);
        table.AddCell(valueCell);
    }

    private PdfPCell CreateHeaderCell(string text, BaseColor bgColor, Font font)
    {
        return new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = bgColor,
            HorizontalAlignment = Element.ALIGN_CENTER,
            Padding = 6,
            BorderColor = BaseColor.WHITE
        };
    }

    private PdfPCell CreateCell(string text, Font font)
    {
        return new PdfPCell(new Phrase(text, font))
        {
            Padding = 5,
            BorderColor = new BaseColor(220, 220, 220)
        };
    }
}