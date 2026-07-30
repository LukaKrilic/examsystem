namespace ExamSystem.Web.Dtos;

// Timestamp is DateTimeOffset, not DateTime — this is a real instant (when the screenshot was
// taken), unlike the naive-Zagreb-local Timestamp on ActiveExamsRequest. A bare DateTime here would
// get assigned into Screenshot.TakenAt (DateTimeOffset) via an implicit conversion that uses the
// *server's* local system offset, silently corrupting the value on any server not set to Zagreb time.
public record ScreenshotRequest(
    string SessionId,
    DateTimeOffset Timestamp,
    ScreenshotStudentDto Student,
    ScreenshotExamDto Exam,
    string Image);

public record ScreenshotStudentDto(string StudentId, string FullName, string Jmbag);

public record ScreenshotExamDto(string ExamId, string Classroom);

public record ScreenshotResponse(long ScreenshotId, string ImagePath);
