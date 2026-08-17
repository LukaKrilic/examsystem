using System.Globalization;
using System.Security.Claims;
using ExamSystem.Web.Auth;
using ExamSystem.Web.Data;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Filters;
using ExamSystem.Web.Infoeduka;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(o => o.Filters.Add<UnknownStudentExceptionFilter>())
    .ConfigureApiBehaviorOptions(o =>
    {
        // Same {error, message} shape as ApiExceptionHandler, for model-binding/validation failures
        // that never reach an action (caught by [ApiController]'s automatic 400 behavior).
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var message = string.Join("; ", ctx.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .SelectMany(kv => kv.Value!.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}")));
            return new BadRequestObjectResult(new { error = "VALIDATION_ERROR", message });
        };
    });

// EF Core maps to the DB owned by database/Database.sql — it never creates or migrates schema.
builder.Services.AddDbContext<ExamDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("ExamDb")));

// The only door to Infoeduka data. Typed + pooled; swapping in the real system later touches this
// line and one class, nothing else.
builder.Services.AddHttpClient<IInfoedukaClient, HttpInfoedukaClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Infoeduka:BaseUrl"]!));

builder.Services.AddScoped<SamlUserService>();
builder.Services.AddScoped<InstructionService>();
builder.Services.AddScoped<ExamQueryService>();
builder.Services.AddScoped<ExamDetailsService>();
builder.Services.AddScoped<AccessCodeService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<ScreenshotService>();
builder.Services.AddScoped<InfoedukaExportService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddHostedService<SessionAutoCloser>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Three schemes: cookie (the session), Saml2 (signs into the cookie), ApiKey (machine clients).
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;  // APIs get 401, not a redirect
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddSaml2(o =>
    {
        var saml = builder.Configuration.GetSection("Saml");
        o.SPOptions.EntityId = new EntityId(saml["EntityId"]);
        // Dev Keycloak's shared realm descriptor always advertises WantAuthnRequestsSigned="true"
        // (a realm-wide flag, not per-client) even with "Client signature required" off. AAI@EduHr
        // in prod doesn't require signed AuthnRequests, so this stays Never; add a signing cert and
        // flip to Always only if the real IdP ever requires it.
        o.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Never;
        o.IdentityProviders.Add(new IdentityProvider(
            new EntityId(saml["IdpEntityId"]), o.SPOptions)
        {
            MetadataLocation = saml["IdpMetadata"],
            LoadMetadata = true
        });
        // prod: add the SP signing certificate here (AAI requires signed requests):
        // o.SPOptions.ServiceCertificates.Add(new X509Certificate2(...));
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ApiOrSaml", p => p
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, "ApiKey")
        .RequireAuthenticatedUser());
    o.AddPolicy("ApiKeyOnly", p => p
        .AddAuthenticationSchemes("ApiKey")
        .RequireAuthenticatedUser());
    o.AddPolicy("SamlOnly", p => p
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();               // everything requires auth by default
});

// i18n: hr (default) and en, persisted in the culture cookie.
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = new[] { new CultureInfo("hr"), new CultureInfo("en") };
    o.DefaultRequestCulture = new RequestCulture("hr");
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
    o.RequestCultureProviders = [new CookieRequestCultureProvider()];
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// The ExceptionHandlerOptions overload (not the plain-string one) is required for the registered
// IExceptionHandler services to run at all. Registered unconditionally (not just in production) so
// ApiExceptionHandler runs in every environment, including under WebApplicationFactory tests (which
// default to "Development") — otherwise /api/** errors would never get the {error, message} shape
// while testing. It returns false for non-API paths, which then fall through to "/Home/Error".
app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/Home/Error" });
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

// No scaffold landing page — "/" goes straight to the AAI@EduHr login screen.
app.MapGet("/", () => Results.Redirect("/login")).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Phase 2 temporary check: proves the backend reads seed data through the DbContext.
// Remove once the real API lands (Phase 4). Development-only so it never ships.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/db-check", async (ExamDbContext db) => Results.Ok(new
    {
        students = await db.Students.CountAsync(),
        courses = await db.Courses.CountAsync(),
        exams = await db.Exams.CountAsync(),
        outcomes = await db.LearningOutcomes.CountAsync(),
        points = await db.StudentOutcomePoints.CountAsync(),
        instructions = await db.Instructions.CountAsync()
    }));

    // Debug endpoint: prints every claim on the current principal, so the SAML attribute mapping
    // (hrEduPersonUniqueID, hrEduPersonUniqueNumber, ...) can be inspected after a Keycloak login.
    app.MapGet("/whoami", (HttpContext ctx) => Results.Ok(new
    {
        isAuthenticated = ctx.User.Identity?.IsAuthenticated ?? false,
        authenticationType = ctx.User.Identity?.AuthenticationType,
        claims = ctx.User.Claims.Select(c => new { c.Type, c.Value })
    }));

#if DEBUG
    // Test-only: mints a cookie session carrying the same claims a real SAML assertion would, so
    // WebApplicationFactory contract tests can exercise SamlOnly-protected endpoints (e.g.
    // /api/sessions/confirm) without driving an actual SAML round-trip through Keycloak.
    // #if DEBUG, not just IsDevelopment() — this mints a session for ANY principal with no secret,
    // so it must not exist in the binary at all once published Release (a misconfigured
    // ASPNETCORE_ENVIRONMENT would otherwise be the only thing standing between this and a full
    // auth bypass).
    app.MapPost("/test/sign-in", async (HttpContext ctx, string principal, string? jmbag) =>
    {
        var claims = new List<Claim> { new("hrEduPersonUniqueID", principal) };
        if (jmbag is not null) claims.Add(new Claim("hrEduPersonUniqueNumber", jmbag));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Ok();
    }).AllowAnonymous();   // the global fallback policy requires auth on everything by default
#endif
}

app.Run();

public partial class Program;
