using System.Globalization;
using ExamSystem.Web.Auth;
using ExamSystem.Web.Data;
using ExamSystem.Web.Filters;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(o => o.Filters.Add<UnknownStudentExceptionFilter>());

// EF Core maps to the DB owned by database/Database.sql — it never creates or migrates schema.
builder.Services.AddDbContext<ExamDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("ExamDb")));

builder.Services.AddScoped<SamlUserService>();
builder.Services.AddScoped<InstructionService>();

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

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
}

app.Run();

public partial class Program;
