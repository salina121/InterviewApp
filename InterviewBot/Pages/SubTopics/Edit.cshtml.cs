using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InterviewBot.Pages.SubTopics
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;

        [BindProperty] public SubTopic SubTopic { get; set; } = new();

        public SelectList? TopicOptions { get; set; }

        public EditModel(AppDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var subTopic = await _db.SubTopics.FindAsync(id);
            if (subTopic == null) return NotFound();

            SubTopic = subTopic;
            TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title", subTopic.TopicId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                return Page();
            }

            var topic = await _db.Topics.FindAsync(SubTopic.TopicId);
            if (topic == null)
            {
                ModelState.AddModelError("SubTopic.TopicId", "Selected topic doesn't exist");
                TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                return Page();
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.SubTopics.Update(SubTopic);  // Changed from Add to Update
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Error saving subtopic: " + ex.Message);
                TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                return Page();
            }
        }
    }
}