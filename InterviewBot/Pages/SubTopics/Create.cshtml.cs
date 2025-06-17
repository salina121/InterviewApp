using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InterviewBot.Pages.SubTopics
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        [BindProperty]
        public SubTopicInputModel SubTopic { get; set; } = new();

        public SelectList? TopicOptions { get; set; }

        public CreateModel(AppDbContext db) => _db = db;

        //public void OnGet()
        //{
        //    TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
        //}


        public async Task OnGetAsync(int? topicId)
        {
            TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");

            // Preselect the topic if topicId is provided
            if (topicId.HasValue)
            {
                SubTopic.TopicId = topicId.Value;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                return Page();
            }

            try
            {
                // Verify the topic exists
                var topicExists = await _db.Topics.AnyAsync(t => t.Id == SubTopic.TopicId);
                if (!topicExists)
                {
                    ModelState.AddModelError("SubTopic.TopicId", "Selected topic doesn't exist");
                    TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                    return Page();
                }

                // Create new subtopic with only the necessary properties
                var newSubTopic = new SubTopic
                {
                    Title = SubTopic.Title,
                    TopicId = SubTopic.TopicId
                };

                _db.SubTopics.Add(newSubTopic);
                await _db.SaveChangesAsync();

                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving subtopic: " + ex.Message);
                TopicOptions = new SelectList(_db.Topics.ToList(), "Id", "Title");
                return Page();
            }
        }

        public class SubTopicInputModel
        {
            [Required]
            [StringLength(100)]
            public string Title { get; set; } = null!;

            [Required]
            public int TopicId { get; set; }
        }
    }
}