//C:\Users\DELL\source\repos\InterviewBot\InterviewBot\Pages\InterviewSessions\Results.cshtml.cs
// Pages/InterviewSessions/Results.cshtml.cs
using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InterviewBot.Pages.InterviewSessions
{
    [Authorize]
    public class ResultsModel : PageModel
    {
        private readonly AppDbContext _db;

        [BindProperty]
        public InterviewSession Session { get; set; } = null!;

        public ResultsModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized();
            }

            Session = await _db.InterviewSessions
                .Include(s => s.SubTopic)
                    .ThenInclude(st => st.Topic)
                .Include(s => s.Result)
                    .ThenInclude(r => r!.Questions)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (Session == null)
            {
                return NotFound();
            }

            if (!Session.IsCompleted)
            {
                return RedirectToPage("/InterviewSessions/Index");
            }

            return Page();
        }
    }
}
