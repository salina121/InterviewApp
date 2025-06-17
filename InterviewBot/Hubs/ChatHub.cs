using Microsoft.AspNetCore.SignalR;
using InterviewBot.Models;
using InterviewBot.Data;
using InterviewBot.Services;
using System.Collections.Concurrent;

public class InterviewQuestionTracker
{
    public List<string> AskedQuestions { get; } = new();
    public List<string> AvailableQuestions { get; } = new();

    public void MarkQuestionAsked(string question)
    {
        AskedQuestions.Add(question);
        AvailableQuestions.Remove(question);
    }
}

public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly GeminiAgentService _gemini;
    private static readonly ConcurrentDictionary<string, InterviewSession> _sessions = new();
    private static readonly ConcurrentDictionary<string, InterviewQuestionTracker> _questionTrackers = new();

    public ChatHub(AppDbContext db, GeminiAgentService gemini)
    {
        _db = db;
        _gemini = gemini;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_sessions.TryRemove(Context.ConnectionId, out var session))
        {
            session.EndTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            Console.WriteLine($"Session ended for connection: {Context.ConnectionId}");
        }
        _questionTrackers.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartInterview(int subTopicId)
    {
        try
        {
            if (_sessions.ContainsKey(Context.ConnectionId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Interview already in progress");
                return;
            }

            var subTopic = await _db.SubTopics.FindAsync(subTopicId);
            if (subTopic == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Invalid subtopic selected");
                return;
            }

            var session = new InterviewSession
            {
                SubTopicId = subTopicId,
                StartTime = DateTime.UtcNow,
                Summary = string.Empty,
                Messages = new List<ChatMessage>(),
                CurrentQuestionNumber = 0,
                IsCompleted = false
            };

            _db.InterviewSessions.Add(session);
            await _db.SaveChangesAsync();

            _sessions.TryAdd(Context.ConnectionId, session);
            await InitializeQuestionTracker(session);

            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer",
                "Welcome to your mock interview! Let's start with some basic information. What is your full name?");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartInterview: {ex}");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "Failed to start interview. Please try again.");
        }
    }

    private async Task InitializeQuestionTracker(InterviewSession session)
    {
        var tracker = new InterviewQuestionTracker();

        var questions = await _gemini.AskQuestionAsync(
            $"Generate 15 distinct technical questions about {session.SubTopic.Title} " +
            $"suitable for a candidate with education level {session.CandidateEducation} " +
            $"and {session.CandidateExperience} years experience. Format as a numbered list.");

        tracker.AvailableQuestions.AddRange(questions.Split('\n')
            .Where(q => q.Contains("."))
            .Select(q => q.Substring(q.IndexOf('.') + 1).Trim())
            .ToList());

        _questionTrackers.TryAdd(Context.ConnectionId, tracker);
    }

    public async Task SendAnswer(string answer)
    {
        if (!_sessions.TryGetValue(Context.ConnectionId, out var session))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "No active session found.");
            return;
        }

        // Save user message
        session.Messages.Add(new ChatMessage
        {
            Content = answer,
            IsUserMessage = true,
            SessionId = session.Id,
            Timestamp = DateTime.UtcNow
        });

        // Handle flow
        if (string.IsNullOrEmpty(session.CandidateName))
        {
            session.CandidateName = answer;
            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", "Thank you. What is your email address?");
        }
        else if (string.IsNullOrEmpty(session.CandidateEmail))
        {
            session.CandidateEmail = answer;
            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", "What is your highest education level?");
        }
        else if (string.IsNullOrEmpty(session.CandidateEducation))
        {
            session.CandidateEducation = answer;
            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", "How many years of experience do you have?");
        }
        else if (string.IsNullOrEmpty(session.CandidateExperience))
        {
            session.CandidateExperience = answer;
            await _db.SaveChangesAsync();
            await AskNextQuestion(session);
        }
        else if (session.CurrentQuestionNumber < 10)
        {
            await _db.SaveChangesAsync();
            await AskNextQuestion(session);
        }
        else
        {
            // After last question, show complete button
            await Clients.Caller.SendAsync("ShowCompleteButton");
        }
    }

    public async Task CompleteInterviewManually()
    {
        if (!_sessions.TryGetValue(Context.ConnectionId, out var session))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "No active session found.");
            return;
        }

        if (session.IsCompleted)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", "Interview already completed.");
            return;
        }

        try
        {
            // Mark session as completed
            session.IsCompleted = true;
            session.EndTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Generate evaluation
            var evaluationPrompt = $"Evaluate this interview for {session.CandidateName} about {session.SubTopic.Title}.\n";
            evaluationPrompt += $"Candidate has {session.CandidateEducation} education and {session.CandidateExperience} years experience.\n";
            evaluationPrompt += "Questions and answers:\n";

            var qaPairs = session.Messages
                .Where(m => !m.IsUserMessage)
                .Select(q => new {
                    Question = q.Content,
                    Answer = session.Messages
                        .FirstOrDefault(a => a.IsUserMessage && a.Timestamp > q.Timestamp)?.Content
                        ?? "No answer provided"
                });

            foreach (var pair in qaPairs)
            {
                evaluationPrompt += $"Q: {pair.Question}\nA: {pair.Answer}\n\n";
            }

            evaluationPrompt += "Provide:\n1. Score out of 100 (Score: XX)\n2. Detailed feedback";

            var evaluation = await _gemini.AskQuestionAsync(evaluationPrompt);

            // Parse score
            int score = 0;
            var scoreMatch = System.Text.RegularExpressions.Regex.Match(evaluation, @"Score:\s*(\d+)");
            if (scoreMatch.Success)
            {
                int.TryParse(scoreMatch.Groups[1].Value, out score);
            }

            // Create and save result
            var result = new InterviewResult
            {
                SessionId = session.Id,
                Score = score,
                Evaluation = evaluation,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var pair in qaPairs)
            {
                result.Questions.Add(new InterviewQuestion
                {
                    Question = pair.Question,
                    Answer = pair.Answer
                });
            }

            _db.InterviewResults.Add(result);
            await _db.SaveChangesAsync();

            // Notify client
            await Clients.Caller.SendAsync("InterviewCompleted", score, evaluation);
            await Clients.Caller.SendAsync("RedirectToResults", session.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error completing interview: {ex}");
            await Clients.Caller.SendAsync("ReceiveMessage", "System",
                "Error completing interview. Please check the results page.");
        }
        finally
        {
            _sessions.TryRemove(Context.ConnectionId, out _);
            _questionTrackers.TryRemove(Context.ConnectionId, out _);
        }
    }

    private async Task AskNextQuestion(InterviewSession session)
    {
        if (session.IsCompleted) return;

        if (!_questionTrackers.TryGetValue(Context.ConnectionId, out var tracker))
        {
            await InitializeQuestionTracker(session);
            tracker = _questionTrackers[Context.ConnectionId];
        }

        if (tracker.AvailableQuestions.Count == 0)
        {
            await InitializeQuestionTracker(session);
            tracker = _questionTrackers[Context.ConnectionId];
        }

        session.CurrentQuestionNumber++;
        var random = new Random();
        int index = random.Next(tracker.AvailableQuestions.Count);
        var question = tracker.AvailableQuestions[index];
        tracker.MarkQuestionAsked(question);

        await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", $"Question {session.CurrentQuestionNumber}/10: {question}");

        session.Messages.Add(new ChatMessage
        {
            Content = question,
            IsUserMessage = false,
            SessionId = session.Id,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}