namespace InterviewBot.Models
{
    public class Topic
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public List<SubTopic> SubTopics { get; set; } = new();
    }
}