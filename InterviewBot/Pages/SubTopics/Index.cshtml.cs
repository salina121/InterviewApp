using InterviewBot.Data;
using InterviewBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InterviewBot.Pages.SubTopics
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public List<SubTopicViewModel> SubTopics { get; set; } = new();
        public Topic? CurrentTopic { get; set; }

        public IndexModel(AppDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync(int? topicId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (topicId == null)
            {
                // If no topic is specified, get all subtopics for the user
                var allSubTopics = await _db.SubTopics
                    .Where(st => st.UserId == userId)
                    .Include(st => st.Topic)
                    .Include(st => st.InterviewSessions)
                    .OrderBy(st => st.Topic.Title).ThenBy(st => st.Title)
                    .ToListAsync();
                
                SubTopics = allSubTopics.Select(st =>
                {
                    var latestSession = st.InterviewSessions.OrderByDescending(s => s.StartTime).FirstOrDefault();
                    return new SubTopicViewModel
                    {
                        Id = st.Id,
                        Title = st.Title,
                        TopicName = st.Topic.Title,
                        IsInterviewCompleted = latestSession?.IsCompleted ?? false,
                        CompletedSessionId = latestSession?.IsCompleted == true ? latestSession.Id : null,
                        LatestSessionId = latestSession?.Id,
                        HasIncompleteSession = latestSession != null && !latestSession.IsCompleted && (latestSession.EndTime.HasValue || latestSession.CurrentQuestionNumber > 0),
                        IsNewInterview = latestSession == null
                    };
                }).ToList();

                return Page();
            }

            CurrentTopic = await _db.Topics.FindAsync(topicId);

            if (CurrentTopic == null || CurrentTopic.UserId != userId)
            {
                return NotFound();
            }

            var subTopicsFromDb = await _db.SubTopics
                .Where(st => st.TopicId == topicId && st.UserId == userId)
                .Include(st => st.InterviewSessions)
                .ToListAsync();

            SubTopics = subTopicsFromDb.Select(st =>
            {
                var latestSession = st.InterviewSessions.OrderByDescending(s => s.StartTime).FirstOrDefault();
                return new SubTopicViewModel
                {
                    Id = st.Id,
                    Title = st.Title,
                    TopicName = CurrentTopic.Title,
                    IsInterviewCompleted = latestSession?.IsCompleted ?? false,
                    CompletedSessionId = latestSession?.IsCompleted == true ? latestSession.Id : null,
                    LatestSessionId = latestSession?.Id,
                    HasIncompleteSession = latestSession != null && !latestSession.IsCompleted && (latestSession.EndTime.HasValue || latestSession.CurrentQuestionNumber > 0),
                    IsNewInterview = latestSession == null
                };
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, int? topicId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var subTopic = await _db.SubTopics.FirstOrDefaultAsync(st => st.Id == id && st.UserId == userId);

            if (subTopic != null)
            {
                _db.SubTopics.Remove(subTopic);
                await _db.SaveChangesAsync();
            }

            return RedirectToPage(new { topicId });
        }
    }

    public class SubTopicViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        public bool IsInterviewCompleted { get; set; }
        public int? CompletedSessionId { get; set; }
        public int? LatestSessionId { get; set; }
        public bool HasIncompleteSession { get; set; }
        public bool IsNewInterview { get; set; }
    }
}