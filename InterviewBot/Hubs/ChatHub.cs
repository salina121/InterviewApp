using Microsoft.AspNetCore.SignalR;
using InterviewBot.Models;
using InterviewBot.Data;
using InterviewBot.Services;
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

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
            try
            {
                session.EndTime = DateTime.UtcNow;
                if (!session.IsCompleted)
                {
                    session.IsCompleted = true;
                    session.Summary = "Disconnected before completion";
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving disconnected session: {ex}");
            }
        }
        _questionTrackers.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartInterview(int subTopicId, string language = "en")
    {
        try
        {
            if (_sessions.ContainsKey(Context.ConnectionId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Interview already in progress");
                return;
            }

            var subTopic = await _db.SubTopics.Include(st => st.Topic).FirstOrDefaultAsync(st => st.Id == subTopicId);
            if (subTopic == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Invalid subtopic selected");
                return;
            }

            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "User not authenticated");
                return;
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "User not found");
                return;
            }

            // Set the language for the interview session
            var interviewLanguage = language == "es" ? InterviewLanguage.Spanish : InterviewLanguage.English;

            var session = new InterviewSession
            {
                SubTopicId = subTopicId,
                StartTime = DateTime.UtcNow,
                Summary = string.Empty,
                Messages = new List<ChatMessage>(),
                CurrentQuestionNumber = 0,
                IsCompleted = false,
                UserId = userId,
                CandidateName = user.FullName,
                CandidateEmail = user.Email,
                CandidateEducation = user.Education ?? "Not specified",
                CandidateExperience = user.Experience ?? "0",
                SubTopic = subTopic,
                Language = interviewLanguage
            };

            _db.InterviewSessions.Add(session);
            await _db.SaveChangesAsync();

            _sessions.TryAdd(Context.ConnectionId, session);
            await InitializeQuestionTracker(session);

            // Send welcome message in the appropriate language
            var welcomeMessage = interviewLanguage == InterviewLanguage.Spanish 
                ? "¡Hola! Bienvenido a tu entrevista simulada. Di hola para comenzar."
                : "Hello! Welcome to your mock interview. Say hello to begin.";

            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", welcomeMessage);
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
        var subTopicDescription = session.SubTopic.Description;
        var topicObjectives = session.SubTopic.Topic?.Objectives;

        // Determine the language for question generation
        var isSpanish = session.Language == InterviewLanguage.Spanish;
        var languageInstruction = isSpanish 
            ? "Generate all questions in Spanish. Respond only in Spanish."
            : "Generate all questions in English. Respond only in English.";

        var prompt = $"{languageInstruction}\n\n" +
                     $"Generate 10 distinct technical questions about {session.SubTopic.Title} " +
                     $"suitable for a candidate with education level {session.CandidateEducation} " +
                     $"and {session.CandidateExperience} years of experience.";

        if (!string.IsNullOrWhiteSpace(subTopicDescription))
        {
            prompt += $" The specific objectives for this sub-topic are: {subTopicDescription}.";
        }
        else if (!string.IsNullOrWhiteSpace(topicObjectives))
        {
            prompt += $" The overall topic objectives are: {topicObjectives}.";
        }

        prompt += " Format the questions as a numbered list.";

        var questions = await _gemini.AskQuestionAsync(prompt);

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

        // If this is the first message (greeting), bot greets and asks first question
        if (session.CurrentQuestionNumber == 0)
        {
            var greetingMessage = session.Language == InterviewLanguage.Spanish
                ? "¡Encantado de conocerte! Comencemos la entrevista."
                : "Nice to meet you! Let's begin the interview.";
            
            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", greetingMessage);
            await AskNextQuestion(session);
            return;
        }

        // Otherwise, continue with next question or complete
        if (session.CurrentQuestionNumber < 10)
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
            Console.WriteLine($"Completing interview for session {session.Id}");
            Console.WriteLine($"In-memory session has {session.Messages.Count} messages");
            
            // First, ensure all messages from the in-memory session are saved to database
            foreach (var message in session.Messages)
            {
                if (message.Id == 0) // New message not yet saved
                {
                    _db.ChatMessages.Add(message);
                }
            }
            await _db.SaveChangesAsync();
            
            // Mark session as completed using raw SQL to avoid tracking issues
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE \"InterviewSessions\" SET \"IsCompleted\" = true, \"EndTime\" = {0} WHERE \"Id\" = {1}",
                DateTime.UtcNow, session.Id);

            // Get the updated session with messages
            var dbSession = await _db.InterviewSessions
                .Include(s => s.Messages)
                .Include(s => s.SubTopic)
                .FirstOrDefaultAsync(s => s.Id == session.Id);

            if (dbSession == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Session not found in database.");
                return;
            }

            Console.WriteLine($"Retrieved session from database. Messages count: {dbSession.Messages.Count}");

            // Generate evaluation
            var isSpanish = dbSession.Language == InterviewLanguage.Spanish;
            var languageInstruction = isSpanish 
                ? "Evalúa esta entrevista en español. Responde solo en español."
                : "Evaluate this interview in English. Respond only in English.";
            
            var evaluationPrompt = $"{languageInstruction}\n\n" +
                                  $"Evaluate this interview for {dbSession.CandidateName} about {dbSession.SubTopic.Title}.\n";
            evaluationPrompt += $"Candidate has {dbSession.CandidateEducation} education and {dbSession.CandidateExperience} years experience.\n";
            evaluationPrompt += "Questions and answers:\n";

            // Properly extract Q&A pairs by pairing questions with their answers
            var messages = dbSession.Messages.OrderBy(m => m.Timestamp).ToList();
            var qaPairs = new List<(string Question, string Answer)>();

            Console.WriteLine($"Total messages in session: {messages.Count}");
            
            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                Console.WriteLine($"Message {i}: IsUser={message.IsUserMessage}, Content='{message.Content}'");
                
                if (!message.IsUserMessage) // This is a bot message
                {
                    Console.WriteLine($"Processing bot message: '{message.Content}'");
                    
                    // Only consider messages that are actual questions (contain "Question" or start with a number)
                    if (message.Content.Contains("Question") || 
                        (message.Content.Contains("/10:") || message.Content.Contains("/10 :")))
                    {
                        Console.WriteLine($"This is a question message: '{message.Content}'");
                        
                        // Find the next user message (answer) after this question
                        var answer = messages.Skip(i + 1)
                            .FirstOrDefault(m => m.IsUserMessage)?.Content ?? "No answer provided";
                        
                        Console.WriteLine($"Found Q&A pair: Q='{message.Content}', A='{answer}'");
                        qaPairs.Add((message.Content, answer));
                    }
                    else
                    {
                        Console.WriteLine($"Skipping non-question bot message: '{message.Content}'");
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping user message: '{message.Content}'");
                }
            }

            Console.WriteLine($"Total Q&A pairs extracted: {qaPairs.Count}");

            foreach (var (question, answer) in qaPairs)
            {
                evaluationPrompt += $"Q: {question}\nA: {answer}\n\n";
            }

            if (qaPairs.Count == 0)
            {
                evaluationPrompt += isSpanish 
                    ? "No se respondieron preguntas en esta entrevista.\n\n"
                    : "No questions were answered in this interview.\n\n";
            }

            evaluationPrompt += isSpanish
                ? "Proporciona:\n1. Puntuación de 0 a 100 (Puntuación: XX)\n2. Retroalimentación detallada"
                : "Provide:\n1. Score out of 100 (Score: XX)\n2. Detailed feedback";

            Console.WriteLine($"Sending evaluation prompt to AI: {evaluationPrompt}");

            var evaluation = await _gemini.AskQuestionAsync(evaluationPrompt);

            // Parse score
            int score = 0;
            var scoreMatch = System.Text.RegularExpressions.Regex.Match(evaluation, @"Score:\s*(\d+)");
            if (!scoreMatch.Success)
            {
                // Try Spanish format
                scoreMatch = System.Text.RegularExpressions.Regex.Match(evaluation, @"Puntuación:\s*(\d+)");
            }
            if (scoreMatch.Success)
            {
                int.TryParse(scoreMatch.Groups[1].Value, out score);
            }

            Console.WriteLine($"Parsed score: {score}");

            // Create and save result
            var result = new InterviewResult
            {
                SessionId = dbSession.Id,
                Score = score,
                Evaluation = evaluation,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var (question, answer) in qaPairs)
            {
                result.Questions.Add(new InterviewQuestion
                {
                    Question = question,
                    Answer = answer
                });
            }

            _db.InterviewResults.Add(result);
            await _db.SaveChangesAsync();

            Console.WriteLine($"Saved result with {result.Questions.Count} questions");

            // Notify client and redirect directly to results
            await Clients.Caller.SendAsync("InterviewCompleted", score, evaluation);
            await Clients.Caller.SendAsync("RedirectToResults", dbSession.Id);
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

    public async Task EndInterviewEarly()
    {
        if (!_sessions.TryGetValue(Context.ConnectionId, out var session))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "No active session found.");
            return;
        }

        try
        {
            // Mark session as completed
            session.IsCompleted = true;
            session.EndTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify client
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "Interview ended early. Your progress has been saved.");
            await Clients.Caller.SendAsync("RedirectToResults", session.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ending interview early: {ex}");
            await Clients.Caller.SendAsync("ReceiveMessage", "System",
                "Error ending interview early. Please try again.");
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

        // Format question in the appropriate language
        var questionPrefix = session.Language == InterviewLanguage.Spanish 
            ? $"Pregunta {session.CurrentQuestionNumber}/10: "
            : $"Question {session.CurrentQuestionNumber}/10: ";
        
        var formattedQuestion = questionPrefix + question;
        await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", formattedQuestion);

        session.Messages.Add(new ChatMessage
        {
            Content = formattedQuestion,
            IsUserMessage = false,
            SessionId = session.Id,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task ResumeInterview(int sessionId)
    {
        try
        {
            // Check if there's already an active session
            if (_sessions.ContainsKey(Context.ConnectionId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Interview already in progress");
                return;
            }

            // Get the session from database
            var session = await _db.InterviewSessions
                .Include(s => s.SubTopic)
                    .ThenInclude(st => st.Topic)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Session not found");
                return;
            }

            // Check if session is already completed
            if (session.IsCompleted)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "This interview is already completed");
                return;
            }

            // Add session to active sessions
            _sessions.TryAdd(Context.ConnectionId, session);
            await InitializeQuestionTracker(session);

            var welcomeBackMessage = session.Language == InterviewLanguage.Spanish
                ? $"¡Bienvenido de vuelta! Continuemos tu entrevista sobre {session.SubTopic.Title}. ¿Dónde nos quedamos?"
                : $"Welcome back! Let's continue your interview on {session.SubTopic.Title}. Where were we?";

            await Clients.Caller.SendAsync("ReceiveMessage", "Interviewer", welcomeBackMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resuming interview: {ex}");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "Failed to resume interview. Please try again.");
        }
    }
}