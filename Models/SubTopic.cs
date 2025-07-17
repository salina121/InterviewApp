using System.ComponentModel.DataAnnotations;

namespace InterviewBot.Models
{
    public class SubTopic
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }
        public string? CandidateEmail { get; set; }

        public int TopicId { get; set; }
        public Topic Topic { get; set; } = null!;
        public List<InterviewSession> InterviewSessions { get; set; } = new();
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}