using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages
{
    public class ChatModel : PageModel
    {
        private readonly AppDbContext _db;

        public ChatModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int SubTopicId { get; set; }

        public SubTopic SubTopic { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var subTopic = await _db.SubTopics
                    .Include(st => st.Topic)
                    .FirstOrDefaultAsync(st => st.Id == SubTopicId);

                if (subTopic == null)
                {
                    return NotFound();
                }

                SubTopic = subTopic;
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading subtopic: {ex}");
                return RedirectToPage("/Error");
            }
        }
    }
}
