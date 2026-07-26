namespace ExamSystem.Web.Dtos;

public record ActiveExamsRequest(string StudentId, DateTime Timestamp);

public record ActiveExamsResponse(StudentDto Student, QueryDto Query, List<ExamDto> Exams);

public record QueryDto(DateTime Timestamp, WindowDto Window);

public record WindowDto(DateTime From, DateTime To);
