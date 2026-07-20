using ExamSystem.Web.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// EF Core maps to the DB owned by database/Database.sql — it never creates or migrates schema.
builder.Services.AddDbContext<ExamDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("ExamDb")));

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
}

app.Run();
