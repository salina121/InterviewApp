using DinkToPdf;
using DinkToPdf.Contracts;
using InterviewBot.Data;
using InterviewBot.Models;
using InterviewBot.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConverter, SynchronizedConverter>(s =>
    new SynchronizedConverter(new PdfTools()));

// Register your custom PdfService
builder.Services.AddScoped<PdfService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options => {
    var supportedCultures = new[] { "en", "es" };
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Add services to the container
builder.Services.AddRazorPages()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();


builder.Services.AddMemoryCache();
// Add this with your other service registrations
builder.Services.AddScoped<GeminiAgentService>();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication with persistent cookie store
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "InterviewBot.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.SessionStore = new MemoryCacheTicketStore();
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Razor Pages configuration
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Account/GuestLogin");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// Add SignalR service
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
});

// Add CORS policy (adjust for production)
builder.Services.AddCors(options => {
    options.AddPolicy("SignalRCors", policy => {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

var supportedCultures = new[] { "en", "es" };

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);


// Configure the HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.MapGet("/testgemini", async (GeminiAgentService gemini) => {
    try
    {
        var response = await gemini.AskQuestionAsync("Hello");
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
});
// In your app configuration (after UseRouting)
app.UseCors("SignalRCors");
app.UseRequestLocalization();

// Map your SignalR hub
app.MapHub<ChatHub>("/chatHub");
app.UseWebSockets();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    SupportedCultures = new[] { new CultureInfo("en"), new CultureInfo("es") },
    SupportedUICultures = new[] { new CultureInfo("en"), new CultureInfo("es") },
    DefaultRequestCulture = new RequestCulture("en")
});

app.UseAuthentication();
app.UseAuthorization();

// Debug endpoints
app.MapGet("/debug/auth", (HttpContext context) =>
    Results.Json(new
    {
        IsAuthenticated = context.User.Identity?.IsAuthenticated,
        UserName = context.User.Identity?.Name,
        Claims = context.User.Claims.Select(c => new { c.Type, c.Value })
    }));

app.MapRazorPages();
await InitializeDatabase(app);
app.Run();

async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Ticket store for persistent sessions
public class MemoryCacheTicketStore : ITicketStore
{
    private readonly IMemoryCache _cache;
    private const string KeyPrefix = "AuthSession-";

    public MemoryCacheTicketStore()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        _cache.TryGetValue(key, out AuthenticationTicket? ticket);
        return Task.FromResult<AuthenticationTicket?>(ticket);
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid();
        var options = new MemoryCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;

        if (expiresUtc.HasValue)
            options.SetAbsoluteExpiration(expiresUtc.Value);

        options.SetSlidingExpiration(TimeSpan.FromDays(1));
        _cache.Set(key, ticket, options);

        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new MemoryCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;

        if (expiresUtc.HasValue)
            options.SetAbsoluteExpiration(expiresUtc.Value);

        options.SetSlidingExpiration(TimeSpan.FromDays(1));
        _cache.Set(key, ticket, options);

        return Task.CompletedTask;
    }
}