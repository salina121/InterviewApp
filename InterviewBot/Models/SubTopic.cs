using System.ComponentModel.DataAnnotations;

namespace InterviewBot.Models
{
    public class SubTopic
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = null!;

        public int TopicId { get; set; }
        public Topic Topic { get; set; } = null!;
        public List<InterviewSession> InterviewSessions { get; set; } = new();
    }
}