using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InterviewBot.Pages.Topics
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;
        [BindProperty] public Topic Topic { get; set; } = new();

        public EditModel(AppDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var topic = await _db.Topics.FindAsync(id);
            if (topic == null) return NotFound();

            Topic = topic;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            _db.Topics.Update(Topic);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}