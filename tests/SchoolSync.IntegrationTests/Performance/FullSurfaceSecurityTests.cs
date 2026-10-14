using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SchoolSync.IntegrationTests.Performance;

/// <summary>
/// CT-701 — the full-suite security sweep. 09-security-spec.md.
///
/// Every phase gate checked authorization for the endpoints that phase added. This checks the
/// WHOLE surface at once, which is a different question: the per-phase tests can all pass while a
/// single endpoint added in some phase carries no [Authorize] at all. These are reflection sweeps
/// over the shipped assembly, so an endpoint added later is covered the day it is written — a
/// hand-maintained list of routes would rot immediately.
/// </summary>
public sealed class FullSurfaceSecurityTests(ITestOutputHelper output)
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private static IEnumerable<(Type Controller, MethodInfo Action)> AllActions() =>
        ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m => (Controller: t, Action: m)));

    /// <summary>
    /// NFR-014/NFR-015: authentication is required for ALL business functions; only public product
    /// information and non-identifying aggregate figures are open. So every action must resolve to
    /// either an [Authorize] or a deliberate, visible [AllowAnonymous] — never to neither.
    /// </summary>
    [Fact]
    public void Every_endpoint_is_explicitly_authorized_or_explicitly_anonymous()
    {
        var unprotected = new List<string>();
        var anonymous = new List<string>();

        foreach (var (controller, action) in AllActions())
        {
            var name = controller.Name + "." + action.Name;
            var allowsAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                                  || controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var authorized = action.GetCustomAttribute<AuthorizeAttribute>() is not null
                             || controller.GetCustomAttribute<AuthorizeAttribute>() is not null;

            if (allowsAnonymous) anonymous.Add(name);
            else if (!authorized) unprotected.Add(name);
        }

        output.WriteLine($"endpoints swept: {AllActions().Count()}");
        output.WriteLine($"explicitly anonymous ({anonymous.Count}): {string.Join(", ", anonymous.OrderBy(x => x))}");
        Assert.True(unprotected.Count == 0,
            "NFR-014/015: these endpoints carry neither [Authorize] nor [AllowAnonymous]: " + string.Join(", ", unprotected));
    }

    /// <summary>
    /// The anonymous surface is the one that needs a named justification, so it is pinned here.
    /// Adding an [AllowAnonymous] endpoint fails this test until someone updates the list, which is
    /// the point: widening the public surface should be a deliberate, reviewed act (NFR-016 allows
    /// only the public directory and non-identifying aggregates).
    /// </summary>
    [Fact]
    public void The_anonymous_surface_is_exactly_the_approved_set()
    {
        var expected = new[]
        {
            "AuthController.Login",
            "AuthController.Refresh",
            "AuthController.Register",
            "DashboardController.PlatformSummary",   // API-049, non-identifying aggregates (ADR-0006)
            "PublicInstitutionDirectoryController.Search", // NFR-016's single bounded exception
            "SubscriptionsController.Enquire",             // API-062 public enquiry
            "SubscriptionsController.Plans"                // API-061 public plan list
        };

        var actual = AllActions()
            .Where(x => x.Action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                        || x.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(x => x.Controller.Name + "." + x.Action.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal).ToArray(), actual);
    }

    /// <summary>
    /// 09-security-spec.md: every audited action writes its audit event. CT-003 made the
    /// SaveChanges interceptor the sole writer, driven by the IAuditable marker — so the
    /// suite-wide question is whether every entity the data model calls audited actually carries
    /// the marker. An entity silently losing IAuditable would disable its audit trail with no
    /// test failing anywhere else.
    /// </summary>
    [Fact]
    public void Every_audited_entity_still_carries_the_IAuditable_marker()
    {
        var expectedAudited = new[]
        {
            typeof(Student), typeof(Teacher), typeof(Class), typeof(AttendanceRecord),
            typeof(Announcement), typeof(Message), typeof(InstitutionConfiguration),
            typeof(Institution), typeof(SubscriptionPlan)
        };

        var missing = expectedAudited.Where(t => !typeof(IAuditable).IsAssignableFrom(t)).Select(t => t.Name).ToList();
        Assert.True(missing.Count == 0, "these entities no longer carry IAuditable, so they write no audit rows: " + string.Join(", ", missing));

        var audited = typeof(SchoolSyncDbContext).Assembly.GetTypes()
            .Concat(typeof(Student).Assembly.GetTypes())
            .Where(t => typeof(IAuditable).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => t.Name).Distinct().OrderBy(x => x);
        output.WriteLine("IAuditable entities: " + string.Join(", ", audited));
    }

    /// <summary>
    /// 12-infrastructure-spec.md CFG-: secret references only, never values. A literal credential
    /// committed to appsettings is the single highest-cost mistake this sweep can catch.
    /// </summary>
    [Fact]
    public void No_shipped_configuration_file_contains_a_literal_secret()
    {
        var apiRoot = FindRepoRoot();
        var configs = Directory.GetFiles(Path.Combine(apiRoot, "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(configs);

        var offenders = new List<string>();
        foreach (var file in configs)
        {
            var text = File.ReadAllText(file);
            // A signing key or password with an actual value on the right-hand side.
            foreach (var key in new[] { "SigningKey", "Password", "ClientSecret", "AccessKey", "ConnectionString" })
            {
                var pattern = new System.Text.RegularExpressions.Regex(
                    "\"" + key + "\"\\s*:\\s*\"(?<value>[^\"]*)\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in pattern.Matches(text))
                {
                    var value = m.Groups["value"].Value;
                    if (value.Length > 0 && !value.StartsWith('@') && !value.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase))
                        offenders.Add(Path.GetFileName(file) + ": " + key);
                }
            }
        }

        output.WriteLine($"configuration files swept: {configs.Count}");
        Assert.True(offenders.Count == 0, "literal secret values found in shipped configuration: " + string.Join(", ", offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
