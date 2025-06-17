using InterviewBot.Data;
using InterviewBot.Models;
using InterviewBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages.InterviewSessions
{
    [Authorize]
    public class ExportModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly PdfService _pdfService;

        public ExportModel(AppDbContext db, PdfService pdfService)
        {
            _db = db;
            _pdfService = pdfService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var session = await _db.InterviewSessions
                .Include(s => s.SubTopic)
                .Include(s => s.Result)
                    .ThenInclude(r => r.Questions)
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null || !session.IsCompleted)
            {
                return NotFound();
            }

            var html = $@"
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                    h1 {{ color: #2c3e50; border-bottom: 2px solid #2c3e50; }}
                    h2 {{ color: #3498db; }}
                    .question {{ margin-bottom: 20px; }}
                    .answer {{ margin-left: 20px; color: #555; }}
                </style>
                <h1>Interview Report: {session.SubTopic.Title}</h1>
                <h3>Candidate: {session.CandidateName}</h3>
                <p><strong>Email:</strong> {session.CandidateEmail}</p>
                <p><strong>Education:</strong> {session.CandidateEducation}</p>
                <p><strong>Experience:</strong> {session.CandidateExperience} years</p>
                <p><strong>Completed:</strong> {session.EndTime?.ToString("f")}</p>
                <p><strong>Score:</strong> {session.Result?.Score}/100</p>
                <hr>
                <h2>Evaluation</h2>
                <div>{session.Result?.Evaluation?.Replace("\n", "<br>")}</div>
                <hr>
                <h2>Questions & Answers</h2>";

            foreach (var qa in session.Result?.Questions ?? new List<InterviewQuestion>())
            {
                html += $@"
                    <div class='question'>
                        <strong>Q:</strong> {qa.Question}
                        <div class='answer'><strong>A:</strong> {qa.Answer}</div>
                    </div>";
            }

            try
            {
                var pdfBytes = _pdfService.GeneratePdf(html);
                return File(pdfBytes, "application/pdf", $"InterviewReport_{id}.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF generation error: {ex}");
                return BadRequest("Failed to generate PDF");
            }
        }
    }
}