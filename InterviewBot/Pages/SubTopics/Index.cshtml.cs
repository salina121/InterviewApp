using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages.SubTopics
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public List<SubTopic> SubTopics { get; set; } = new();

        public IndexModel(AppDbContext db) => _db = db;

        public async Task OnGetAsync()
        {
            SubTopics = await _db.SubTopics.Include(st => st.Topic).ToListAsync();
        }
    }
}