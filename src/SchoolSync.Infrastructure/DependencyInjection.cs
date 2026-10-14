using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SchoolSync.Application.Auth;
using SchoolSync.Infrastructure.Auth;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SchoolSyncDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SchoolSync"));
            // 07-business-logic/_shared.md §3: the interceptor is the ONLY writer of AuditLog
            // rows for entity changes — registered here so every SaveChanges through this
            // context, from any caller, goes through it. Resolved from `sp` (not `new`'d) so it
            // gets the real, request-scoped IAuditContext.
            options.AddInterceptors(sp.GetRequiredService<AuditLoggingInterceptor>());
        });

        // The real, request-scoped ICurrentTenant, resolved from the JWT institution_id claim
        // (CT-001 deliberately shipped with none registered so a missing registration would
        // fail fast rather than silently permit an unscoped query).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
        services.AddScoped<IAuditContext, HttpAuditContext>();
        services.AddScoped<AuditLoggingInterceptor>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<SchoolSync.Application.Institutions.IInstitutionRepository, SchoolSync.Infrastructure.Institutions.InstitutionRepository>();
        services.AddScoped<SchoolSync.Application.Institutions.IInstitutionService, SchoolSync.Application.Institutions.InstitutionService>();
        services.AddScoped<SchoolSync.Application.Students.IStudentRepository, SchoolSync.Infrastructure.Students.StudentRepository>();
        services.AddScoped<SchoolSync.Application.Students.IStudentService, SchoolSync.Application.Students.StudentService>();
        // Stateless field rules, like IPasswordPolicy — no per-request state to carry.
        services.AddSingleton<SchoolSync.Application.Students.IStudentFieldPolicy, SchoolSync.Application.Students.StudentFieldPolicy>();
        services.AddScoped<SchoolSync.Application.ParentLinks.IParentLinkRepository, SchoolSync.Infrastructure.ParentLinks.ParentLinkRepository>();
        services.AddScoped<SchoolSync.Application.ParentLinks.IParentLinkService, SchoolSync.Application.ParentLinks.ParentLinkService>();
        services.AddSingleton<SchoolSync.Application.ParentLinks.IParentLinkCodeGenerator, SchoolSync.Application.ParentLinks.ParentLinkCodeGenerator>();
        services.AddScoped<SchoolSync.Application.Teachers.ITeacherRepository, SchoolSync.Infrastructure.Teachers.TeacherRepository>();
        services.AddScoped<SchoolSync.Application.Teachers.ITeacherService, SchoolSync.Application.Teachers.TeacherService>();
        services.AddScoped<SchoolSync.Application.Classes.IClassRepository, SchoolSync.Infrastructure.Classes.ClassRepository>();
        services.AddScoped<SchoolSync.Application.Classes.IClassService, SchoolSync.Application.Classes.ClassService>();
        services.AddScoped<SchoolSync.Application.Attendance.IAttendanceRepository, SchoolSync.Infrastructure.Attendance.AttendanceRepository>();
        services.AddScoped<SchoolSync.Application.Attendance.IAttendanceService, SchoolSync.Application.Attendance.AttendanceService>();
        services.AddScoped<SchoolSync.Application.Common.IIdempotencyStore, SchoolSync.Infrastructure.Common.IdempotencyStore>();
        services.AddScoped<SchoolSync.Application.Communication.IAnnouncementRepository, SchoolSync.Infrastructure.Communication.AnnouncementRepository>();
        services.AddScoped<SchoolSync.Application.Communication.IAnnouncementService, SchoolSync.Application.Communication.AnnouncementService>();
        services.AddScoped<SchoolSync.Application.Communication.IRoomRepository, SchoolSync.Infrastructure.Communication.RoomRepository>();
        services.AddScoped<SchoolSync.Application.Communication.IRoomService, SchoolSync.Application.Communication.RoomService>();
        services.AddScoped<SchoolSync.Application.Communication.IMessagingRepository, SchoolSync.Infrastructure.Communication.MessagingRepository>();
        services.AddScoped<SchoolSync.Application.Communication.IMessagingService, SchoolSync.Application.Communication.MessagingService>();
        services.AddScoped<SchoolSync.Application.Communication.IMeetingRepository, SchoolSync.Infrastructure.Communication.MeetingRepository>();
        services.AddScoped<SchoolSync.Application.Communication.IMeetingService, SchoolSync.Application.Communication.MeetingService>();
        // Q-221 / CFG-SEC-04. Resolved lazily from the live IConfiguration (the CT-002 lesson), so a
        // missing ACS_CONNECTION_STRING yields the unconfigured issuer (API-047 -> 503 ERR-903)
        // instead of a startup failure, and a configured one yields the real ACS client.
        services.AddSingleton<SchoolSync.Application.Communication.IMeetingTokenIssuer>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()["ACS_CONNECTION_STRING"];
            return string.IsNullOrWhiteSpace(connectionString)
                ? new SchoolSync.Infrastructure.Communication.UnconfiguredMeetingTokenIssuer()
                : new SchoolSync.Infrastructure.Communication.AcsMeetingTokenIssuer(new Azure.Communication.Identity.CommunicationIdentityClient(connectionString));
        });
        services.AddScoped<SchoolSync.Application.Operations.IDashboardRepository, SchoolSync.Infrastructure.Operations.DashboardRepository>();
        services.AddScoped<SchoolSync.Application.Operations.IDashboardService, SchoolSync.Application.Operations.DashboardService>();
        services.AddScoped<SchoolSync.Application.Operations.ITimetableRepository, SchoolSync.Infrastructure.Operations.TimetableRepository>();
        services.AddScoped<SchoolSync.Application.Operations.ITimetableService, SchoolSync.Application.Operations.TimetableService>();
        services.AddScoped<SchoolSync.Application.Configuration.IInstitutionConfigRepository, SchoolSync.Infrastructure.Configuration.InstitutionConfigRepository>();
        services.AddScoped<SchoolSync.Application.Configuration.IInstitutionConfigService, SchoolSync.Application.Configuration.InstitutionConfigService>();
        services.AddScoped<SchoolSync.Infrastructure.Configuration.AttendanceLockSweepJob>();
        services.AddScoped<SchoolSync.Infrastructure.Configuration.RetentionSweepJob>();
        services.AddScoped<SchoolSync.Application.Configuration.IRolloverRepository, SchoolSync.Infrastructure.Configuration.RolloverRepository>();
        services.AddScoped<SchoolSync.Application.Configuration.IRolloverService, SchoolSync.Application.Configuration.RolloverService>();
        services.AddScoped<SchoolSync.Infrastructure.Configuration.RolloverJobRunner>();
        services.AddScoped<SchoolSync.Infrastructure.Configuration.RolloverJob>();
        services.AddScoped<SchoolSync.Infrastructure.Configuration.YearEndRolloverJob>();
        services.AddScoped<SchoolSync.Application.Commercial.IPlanEntitlements, SchoolSync.Infrastructure.Commercial.PlanEntitlements>();
        // CT-701 / U29: serialises the BRL-073 capacity gate with the write it guards.
        services.AddScoped<SchoolSync.Application.Common.ISerializableTransaction, SchoolSync.Infrastructure.Persistence.SerializableTransaction>();
        // CT-801 / U45: the keyed leadership-code lookup. Singleton because it owns an HMAC key
        // read once at startup - and it THROWS there if CFG-SEC-06 is unset, which is the
        // fail-closed behaviour CT-801's DoD requires. A defaulted HMAC key is not a key.
        services.AddSingleton<SchoolSync.Application.Common.ILeadershipCodeLookup, SchoolSync.Infrastructure.Auth.LeadershipCodeLookup>();
        services.AddScoped<SchoolSync.Application.Commercial.ISubscriptionRepository, SchoolSync.Infrastructure.Commercial.SubscriptionRepository>();
        services.AddScoped<SchoolSync.Application.Commercial.ISubscriptionService, SchoolSync.Application.Commercial.SubscriptionService>();
        services.AddScoped<SchoolSync.Application.Commercial.IEnquiryNotifier, SchoolSync.Infrastructure.Commercial.LoggingEnquiryNotifier>();
        services.AddScoped<SchoolSync.Application.Commercial.ITrialService, SchoolSync.Application.Commercial.TrialService>();
        services.AddScoped<SchoolSync.Infrastructure.Commercial.TrialReversionJob>();
        // CT-402 reports. The job queue and Hangfire itself are wired in the Api layer (Program.cs).
        services.AddScoped<SchoolSync.Application.Operations.IReportRunRepository, SchoolSync.Infrastructure.Operations.ReportRunRepository>();
        services.AddScoped<SchoolSync.Application.Operations.IReportService, SchoolSync.Application.Operations.ReportService>();
        services.AddScoped<SchoolSync.Application.Operations.IReportDataSource, SchoolSync.Infrastructure.Operations.ReportDataSource>();
        services.AddScoped<SchoolSync.Infrastructure.Operations.ReportJobRunner>();
        services.AddScoped<SchoolSync.Infrastructure.Operations.ReportFailureRecorder>();
        services.AddScoped<SchoolSync.Infrastructure.Operations.ReportJob>();
        // Q-222 / CFG-SEC-05, resolved lazily from the live configuration (the CT-002 lesson).
        services.AddSingleton<SchoolSync.Application.Operations.IReportFileStore>(sp =>
        {
            var liveConfiguration = sp.GetRequiredService<IConfiguration>();
            var blobConnection = liveConfiguration["BLOB_STORAGE_CONNECTION_STRING"];
            return string.IsNullOrWhiteSpace(blobConnection)
                ? new SchoolSync.Infrastructure.Operations.LocalDiskReportFileStore(
                    liveConfiguration["Reports:LocalStorePath"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "reports"))
                : new SchoolSync.Infrastructure.Operations.BlobReportFileStore(new Azure.Storage.Blobs.BlobServiceClient(blobConnection));
        });
        services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // Deliberately NOT read eagerly here: this method runs before `builder.Build()`, which is
        // exactly the point a test host (WebApplicationFactory's ConfigureAppConfiguration) injects
        // its own configuration overrides — reading configuration.GetSection(...)["SigningKey"]
        // synchronously at THIS point in Program.cs would capture the pre-override snapshot and
        // silently ignore a test's replacement value. Instead, resolve IConfiguration lazily,
        // through the options pipeline, which only runs when JwtBearerOptions is actually needed.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, liveConfiguration) =>
            {
                var jwtSection = liveConfiguration.GetSection(JwtOptions.SectionName);
                var signingKey = jwtSection["SigningKey"]
                    ?? throw new InvalidOperationException(
                        "Jwt:SigningKey is not configured. Set it via an environment variable or user-secrets — never hardcode it (company.md §4).");

                // Without this, ASP.NET Core's default inbound claim-type map silently renames
                // "sub" to the legacy http://schemas.xmlsoap.org/.../nameidentifier URI (and
                // similarly for other registered claim names), so `User.FindFirstValue("sub")`
                // never matches even though the token genuinely carries a "sub" claim
                // (09-security-spec.md §1's "claims: sub, role, institution_id" is meant
                // literally). Disabling the map keeps the claim names exactly as issued.
                options.MapInboundClaims = false;

                // CT-303: a browser WebSocket cannot send an Authorization header, so the SignalR
                // client passes the same JWT as `access_token` on the hub URL. Accepted ONLY for
                // /hubs/ paths — REST endpoints still require the header, so a token in a URL (and
                // therefore in proxy logs) is never honoured for them.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = token;
                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();

        return services;
    }
}
