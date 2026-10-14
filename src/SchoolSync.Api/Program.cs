using SchoolSync.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// CT-303 / T-001. Q-221: in-process SignalR everywhere; Azure SignalR Service (INF-003, "Default"
// mode, the scale-out backplane) is added only when CFG-SEC-03 is present. Read at startup on
// purpose — the transport cannot be swapped after the host is built, and tests never set it.
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(builder.Configuration["AZURE_SIGNALR_CONNECTION_STRING"]))
    signalR.AddAzureSignalR(builder.Configuration["AZURE_SIGNALR_CONNECTION_STRING"]);
builder.Services.AddSingleton<SchoolSync.Api.Hubs.RoomPresenceTracker>();
builder.Services.AddScoped<SchoolSync.Application.Communication.IRoomEventPublisher, SchoolSync.Api.Hubs.HubRoomEventPublisher>();

// CT-402 / T-005 / Q-222: Hangfire (report generation; CT-501 adds its recurring jobs later).
SchoolSync.Api.Jobs.HangfireSetup.AddBackgroundJobs(builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// CT-601 / 09-security-spec.md trust boundary "Browser ⇄ API": CSP and companion headers on every
// response. connect-src allows this origin (REST + the SignalR WebSocket) and Azure Communication
// Services (the browser Calling SDK, T-006) — nothing else. style-src keeps 'unsafe-inline' because
// Radix primitives set positioning through inline style attributes; no inline <script> is ever allowed.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "font-src 'self' data:; connect-src 'self' wss: https://*.communication.azure.com wss://*.communication.azure.com; " +
        "media-src 'self' blob: mediastream:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=()";
    await next();
});

// Q-231: the React SPA (src/SchoolSync.Web) is built into wwwroot and served from this same origin.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SchoolSync.Api.Hubs.CollaborationHub>(SchoolSync.Application.Communication.RoomService.HubPath);

// Q-231: client-side (path) routes fall back to index.html — but never for /api or /hubs, which must
// keep returning their own 404s rather than an HTML page.
app.MapFallback(async context =>
{
    var path = context.Request.Path;
    var index = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
    if (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs") || !File.Exists(index))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index);
});

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests (CT-002).
public partial class Program;
