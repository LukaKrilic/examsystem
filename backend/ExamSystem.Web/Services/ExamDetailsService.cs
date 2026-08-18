using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;

namespace ExamSystem.Web.Services;

// Pure passthrough to Infoeduka: outcome points are never stored locally, so there is no DbContext
// here at all. Called mid-wizard (the outcome-selection step), i.e. always before confirmation.
public class ExamDetailsService(IInfoedukaClient infoeduka)
{
    public async Task<ExamDetailsResponse> GetDetailsAsync(string studentId, string courseId)
    {
        var student = await infoeduka.GetStudentAsync(studentId)
            ?? throw new StudentNotFoundException(studentId);

        var course = await infoeduka.GetCourseOutcomesAsync(studentId, courseId)
            ?? throw new ExamNotFoundException(courseId);

        var outcomes = course.Outcomes
            .OrderBy(o => o.OutcomeCode)
            .Select(o => new OutcomeDto(
                o.OutcomeCode,
                o.TotalPointsEarned,
                o.TotalPointsMax,
                o.ExamPointsEarned,
                o.ExamPointsMax))
            .ToList();

        return new ExamDetailsResponse(
            new StudentDto(student.StudentId, student.FullName, student.Jmbag),
            new CourseDto(course.CourseId, course.CourseNameHr, course.CourseNameEn),
            outcomes);
    }
}
