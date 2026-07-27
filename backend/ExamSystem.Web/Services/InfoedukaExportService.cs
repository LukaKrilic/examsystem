using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class InfoedukaExportService(ExamDbContext db)
{
    public async Task<ExamStudentsResponse> GetStudentsAsync(string examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.ExamId == examId)
            ?? throw new ExamNotFoundException(examId);

        var registrations = await db.ExamRegistrations
            .Include(r => r.Student)
            .Where(r => r.ExamId == exam.Id)
            .ToListAsync();

        var students = new List<ExamStudentDto>();
        foreach (var reg in registrations)
        {
            var session = await db.ExamSessions
                .Include(s => s.Outcomes).ThenInclude(o => o.LearningOutcome)
                .Where(s => s.StudentId == reg.StudentId && s.ExamId == exam.Id)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            students.Add(new ExamStudentDto(
                reg.Student.StudentId,
                reg.Student.FullName,
                reg.Student.Jmbag,
                session?.GroupNo,
                session?.SessionId,
                session?.StartedAt?.UtcDateTime,
                session?.Outcomes.Select(o => o.LearningOutcome.OutcomeCode).ToList() ?? []));
        }

        return new ExamStudentsResponse(examId, students);
    }
}
