namespace ExamSystem.Web.Dtos;

public record ScreenshotRequest(
    string SessionId,
    DateTime Timestamp,
    ScreenshotStudentDto Student,
    ScreenshotExamDto Exam,
    string Image);

public record ScreenshotStudentDto(string StudentId, string FullName, string Jmbag);

public record ScreenshotExamDto(string ExamId, string Classroom);

public record ScreenshotResponse(long ScreenshotId, string ImagePath);
