using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// The ONE direction where we are the source of truth: this endpoint serves data TO Infoeduka, so it
// stays purely local and never calls IInfoedukaClient.
//
// The roster is therefore every student who has a session for this exam, with their names read from
// the confirm-time snapshot. Students who merely registered but never started a session are not
// listed — we hold no registration table, and Infoeduka already knows who registered.
public class InfoedukaExportService(ExamDbContext db)
{
    public async Task<ExamStudentsResponse> GetStudentsAsync(string examId)
    {
        var sessions = await db.ExamSessions
            .Include(s => s.Outcomes)
            .Where(s => s.ExamId == examId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var students = sessions
            .GroupBy(s => s.StudentId)
            .Select(g => g.First())        // most recent session per student
            .Select(s => new ExamStudentDto(
                s.StudentId,
                s.StudentFullName ?? "",
                s.StudentJmbag ?? "",
                s.GroupNo,
                s.SessionId,
                s.StartedAt?.UtcDateTime,
                s.Outcomes.Select(o => o.OutcomeCode).OrderBy(c => c).ToList()))
            .ToList();

        return new ExamStudentsResponse(examId, students);
    }
}
