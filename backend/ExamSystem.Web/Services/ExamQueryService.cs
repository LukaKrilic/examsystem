using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// Two dependencies, two roles: IInfoedukaClient for the student and the registration list (external
// data, always read live before confirmation), ExamDbContext ONLY for the local exclusion tables.
public class ExamQueryService(IInfoedukaClient infoeduka, ExamDbContext db, ILogger<ExamQueryService> logger)
{
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    public async Task<ActiveExamsResponse> ActiveExamsAsync(string studentId, DateTime ts)
    {
        var student = await infoeduka.GetStudentAsync(studentId)
            ?? throw new StudentNotFoundException(studentId);

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zagreb);
        if (Math.Abs((ts - now).TotalMinutes) > 10)
        {
            logger.LogWarning("Client timestamp {Client} deviates from server time {Server}; using server time", ts, now);
            ts = now;
        }
        var from = ts.AddMinutes(-30);
        var to = ts.AddMinutes(30);
        var fromUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from, Zagreb));
        var toUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to, Zagreb));

        var registrations = await infoeduka.GetRegistrationsAsync(studentId, fromUtc, toUtc);

        var lockedExamIds = await db.LockedExams
            .Where(l => l.StudentId == studentId)
            .Select(l => l.ExamId)
            .ToListAsync();

        // Edge case 5: an exam whose session already FINISHED/AUTO_CLOSED stays hidden too, even
        // still inside the ±30 min window on re-login — only a session with no prior attempt, or one
        // still ACTIVE (resumable), keeps the exam in this list.
        var doneExamIds = await db.ExamSessions
            .Where(s => s.StudentId == studentId && s.Status != SessionStatus.ACTIVE)
            .Select(s => s.ExamId)
            .ToListAsync();

        var examDtos = registrations
            .Where(r => !lockedExamIds.Contains(r.ExamId) && !doneExamIds.Contains(r.ExamId))
            .OrderBy(r => r.ExamDateTime)
            .Select(r => new ExamDto(
                r.ExamId,
                r.CourseNameHr,
                r.CourseNameEn,
                r.CourseId,
                TimeZoneInfo.ConvertTimeFromUtc(r.ExamDateTime.UtcDateTime, Zagreb),
                r.Classroom))
            .ToList();

        return new ActiveExamsResponse(
            new StudentDto(student.StudentId, student.FullName, student.Jmbag),
            new QueryDto(ts, new WindowDto(from, to)),
            examDtos);
    }
}
