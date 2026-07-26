using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class ExamQueryService(ExamDbContext db, ILogger<ExamQueryService> logger)
{
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    public async Task<ActiveExamsResponse> ActiveExamsAsync(string studentId, DateTime ts)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId)
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

        var lockedIds = await db.LockedExams
            .Where(l => l.StudentId == student.Id)
            .Select(l => l.ExamId)
            .ToListAsync();

        // Edge case 5: an exam whose session already FINISHED/AUTO_CLOSED stays hidden too, even
        // still inside the ±30 min window on re-login — only a session with no prior attempt, or one
        // still ACTIVE (resumable), keeps the exam in this list.
        var doneExamIds = await db.ExamSessions
            .Where(s => s.StudentId == student.Id && s.Status != SessionStatus.ACTIVE)
            .Select(s => s.ExamId)
            .ToListAsync();

        var registeredExamIds = await db.ExamRegistrations
            .Where(r => r.StudentId == student.Id)
            .Select(r => r.ExamId)
            .ToListAsync();

        // Include must come before Select/Where-through-navigation, so query Exam directly rather
        // than projecting through ExamRegistration.
        var exams = await db.Exams
            .Include(e => e.Course)
            .Where(e => registeredExamIds.Contains(e.Id)
                     && e.ExamDateTime >= fromUtc && e.ExamDateTime <= toUtc
                     && !lockedIds.Contains(e.Id)
                     && !doneExamIds.Contains(e.Id))
            .OrderBy(e => e.ExamDateTime)
            .ToListAsync();

        var examDtos = exams.Select(e => new ExamDto(
            e.ExamId,
            e.Course.CourseNameHr,
            e.Course.CourseNameEn,
            e.Course.CourseId,
            TimeZoneInfo.ConvertTimeFromUtc(e.ExamDateTime.UtcDateTime, Zagreb),
            e.Classroom)).ToList();

        return new ActiveExamsResponse(
            new StudentDto(student.StudentId, student.FullName, student.Jmbag),
            new QueryDto(ts, new WindowDto(from, to)),
            examDtos);
    }
}
