using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages.Topics
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public List<Topic> Topics { get; set; } = new();

        public IndexModel(AppDbContext db) => _db = db;

        public void OnGet() => Topics = _db.Topics.Include(t => t.SubTopics).ToList();

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var topic = await _db.Topics.FindAsync(id);
            if (topic != null)
            {
                _db.Topics.Remove(topic);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }

}
