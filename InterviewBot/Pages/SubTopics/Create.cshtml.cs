using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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

                // Get the current user's ID
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                var newSubTopic = new SubTopic
                {
                    Title = SubTopic.Title,
                    Description = SubTopic.Description,
                    CandidateEmail = SubTopic.CandidateEmail,
                    TopicId = SubTopic.TopicId,
                    UserId = userId
                };

                _db.SubTopics.Add(newSubTopic);
                await _db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(newSubTopic.CandidateEmail))
                {
                    // TODO: Implement email sending logic here
                    // For example: await _emailSender.SendInterviewInviteAsync(newSubTopic);
                }

                return RedirectToPage("Index", new { topicId = newSubTopic.TopicId });
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

            public string? Description { get; set; }

            [EmailAddress]
            public string? CandidateEmail { get; set; }

            [Required]
            public int TopicId { get; set; }
        }
    }
}