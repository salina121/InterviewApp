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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                Sessions = await _db.InterviewSessions
                    .Include(s => s.SubTopic)
                        .ThenInclude(st => st.Topic)
                    .Include(s => s.Result)
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();
            }
        }
    }
}