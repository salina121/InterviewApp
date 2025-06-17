using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InterviewBot.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public List<Topic>? Topics { get; set; }

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public void OnGet()
        {
            Topics = _db.Topics.Include(t => t.SubTopics).ToList();
            Console.WriteLine($"Index page accessed. Authenticated: {User.Identity.IsAuthenticated}");
        }
    }

}
