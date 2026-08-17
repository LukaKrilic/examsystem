using MockInfoeduka.Api;

// Stand-in for the university's Infoeduka system. Deliberately NOT authenticated: it models an
// external system we don't control, so it is not part of the exam system's security surface. It runs
// on its own port (8090) so the process separation is visible in the code and in the demo.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/students/{studentId}", (string studentId) =>
{
    var student = SeedData.Students.FirstOrDefault(s => s.StudentId == studentId);
    return student is null
        ? Results.NotFound()
        : Results.Ok(new { student.StudentId, student.FullName, student.Jmbag });
});

// Maps a SAML identity to a student record: hrEduPersonUniqueID first, JMBAG only as a fallback.
// A 404 here is what ExamSystem.Web turns into its "account not registered for exams" page.
app.MapGet("/api/students/resolve", (string? aaiPrincipal, string? jmbag) =>
{
    var student =
        (aaiPrincipal is null ? null : SeedData.Students.FirstOrDefault(s => s.AaiPrincipal == aaiPrincipal))
        ?? (jmbag is null ? null : SeedData.Students.FirstOrDefault(s => s.Jmbag == jmbag));

    return student is null
        ? Results.NotFound()
        : Results.Ok(new { student.StudentId, student.FullName, student.Jmbag });
});

// Omit from/to to get the student's FULL registration list — the same-term lockout needs exams that
// sit outside the ±30 min window, so it must not be forced through the windowed call.
app.MapGet("/api/students/{studentId}/registrations", (string studentId, DateTimeOffset? from, DateTimeOffset? to) =>
{
    var examIds = SeedData.Registrations
        .Where(r => r.StudentId == studentId)
        .Select(r => r.ExamId)
        .ToHashSet();

    var exams = SeedData.Exams.Where(e => examIds.Contains(e.ExamId));
    if (from is not null) exams = exams.Where(e => e.ExamDateTime >= from);
    if (to is not null) exams = exams.Where(e => e.ExamDateTime <= to);

    var result = exams.Select(e =>
    {
        var course = SeedData.Courses.First(c => c.CourseId == e.CourseId);
        return new
        {
            e.ExamId,
            e.CourseId,
            courseNameHr = course.NameHr,
            courseNameEn = course.NameEn,
            e.ExamDateTime,
            e.Classroom
        };
    });

    return Results.Ok(result);
});

app.MapGet("/api/students/{studentId}/courses/{courseId}/outcomes", (string studentId, string courseId) =>
{
    if (!SeedData.Students.Any(s => s.StudentId == studentId)) return Results.NotFound();

    var course = SeedData.Courses.FirstOrDefault(c => c.CourseId == courseId);
    if (course is null) return Results.NotFound();

    var outcomes = SeedData.Outcomes
        .Where(o => o.StudentId == studentId && o.CourseId == courseId)
        .Select(o => new
        {
            o.OutcomeCode,
            totalPointsEarned = o.TotalEarned,
            totalPointsMax = o.TotalMax,
            examPointsEarned = o.ExamEarned,
            examPointsMax = o.ExamMax
        });

    return Results.Ok(new { courseId, courseName = course.NameHr, outcomes });
});

app.Run();
