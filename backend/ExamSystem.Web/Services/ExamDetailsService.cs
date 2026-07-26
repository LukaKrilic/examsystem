using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class ExamDetailsService(ExamDbContext db)
{
    public async Task<ExamDetailsResponse> GetDetailsAsync(string studentId, string courseId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId)
            ?? throw new StudentNotFoundException(studentId);

        var course = await db.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId)
            ?? throw new ExamNotFoundException(courseId);

        var outcomes = await db.StudentOutcomePoints
            .Include(p => p.LearningOutcome)
            .Where(p => p.StudentId == student.Id && p.LearningOutcome.CourseId == course.Id)
            .OrderBy(p => p.LearningOutcome.OutcomeCode)
            .Select(p => new OutcomeDto(
                p.LearningOutcome.OutcomeCode,
                p.TotalPointsEarned,
                p.TotalPointsMax,
                p.ExamPointsEarned,
                p.ExamPointsMax))
            .ToListAsync();

        return new ExamDetailsResponse(
            new StudentDto(student.StudentId, student.FullName, student.Jmbag),
            new CourseDto(course.CourseId, course.CourseNameHr, course.CourseNameEn),
            outcomes);
    }
}
