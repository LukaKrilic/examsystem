namespace ExamSystem.Web.Dtos;

public record ExamStudentsRequest(string ExamId);

public record ExamStudentsResponse(string ExamId, List<ExamStudentDto> Students);

public record ExamStudentDto(
    string StudentId,
    string FullName,
    string Jmbag,
    int? GroupNo,
    string? SessionId,
    DateTime? StartedAt,
    List<string> SelectedOutcomes);
