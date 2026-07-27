using System.Text.Json.Serialization;

namespace ExamSystem.Web.Dtos;

public record StudentDto(string StudentId, string FullName, string Jmbag);

public record ExamDto(
    string ExamId,
    [property: JsonPropertyName("courseNameHR")] string CourseNameHR,
    [property: JsonPropertyName("courseNameEN")] string CourseNameEN,
    [property: JsonPropertyName("courseID")]     string CourseID,
    DateTime ExamDateTime,
    string Classroom);
