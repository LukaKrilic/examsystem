using System.Text.Json.Serialization;

namespace ExamSystem.Web.Dtos;

public record ExamDetailsRequest(string StudentId, string CourseId);

public record ExamDetailsResponse(StudentDto Student, CourseDto Course, List<OutcomeDto> Outcomes);

public record CourseDto(
    [property: JsonPropertyName("courseID")]     string CourseID,
    [property: JsonPropertyName("courseNameHR")] string CourseNameHR,
    [property: JsonPropertyName("courseNameEN")] string CourseNameEN);

public record OutcomeDto(
    string OutcomeCode,
    decimal TotalPointsEarned,
    decimal TotalPointsMax,
    decimal ExamPointsEarned,
    decimal ExamPointsMax);
