namespace InterviewBot.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public InterviewSession Session { get; set; } = null!;
        public bool IsUserMessage { get; set; }
        public string Content { get; set; } = null!;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}