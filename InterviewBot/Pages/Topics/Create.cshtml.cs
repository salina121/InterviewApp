// Pages/Topics/Create.cshtml.cs
using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InterviewBot.Pages.Topics
{

    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        [BindProperty]
        public TopicInputModel Topic { get; set; } = new();

        public CreateModel(AppDbContext db)
        {
            _db = db;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var newTopic = new Topic
                {
                    Title = Topic.Title
                };

                _db.Topics.Add(newTopic);
                await _db.SaveChangesAsync();

                // Redirect to add subtopics
                return RedirectToPage("AddSubtopics", new { id = newTopic.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving topic: " + ex.Message);
                return Page();
            }
        }

        public class TopicInputModel
        {
            [Required]
            [StringLength(100)]
            public string Title { get; set; } = null!;
        }
    }
}