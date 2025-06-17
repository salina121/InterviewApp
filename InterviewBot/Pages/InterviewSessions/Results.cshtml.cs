//C:\Users\DELL\source\repos\InterviewBot\InterviewBot\Pages\InterviewSessions\Results.cshtml.cs
// Pages/InterviewSessions/Results.cshtml.cs
using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages.InterviewSessions
{
    public class ResultsModel : PageModel
    {
        private readonly AppDbContext _db;

        [BindProperty]
        public InterviewSession Session { get; set; } = null!;

        public ResultsModel(AppDbContext db)
        {
            _db = db;
        }

        // In Results.cshtml.cs
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Session = await _db.InterviewSessions
                .Include(s => s.SubTopic)
                .Include(s => s.Result)
                    .ThenInclude(r => r!.Questions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (Session == null)
            {
                return NotFound();
            }

            return Page();
        }

    }
}
