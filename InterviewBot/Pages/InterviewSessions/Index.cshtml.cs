
using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages.InterviewSessions
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public List<InterviewSession> Sessions { get; set; } = new();

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnGetAsync()
        {
            Sessions = await _db.InterviewSessions
                .Include(s => s.SubTopic)
                .Include(s => s.Result)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCompleteAsync(int id)
        {
            var session = await _db.InterviewSessions
                .Include(s => s.Result)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return NotFound();
            }

            if (!session.IsCompleted)
            {
                session.IsCompleted = true;
                session.EndTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage("Results", new { id });
        }
    }
}