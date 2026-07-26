namespace ExamSystem.Web.Dtos;

public record SessionConfirmRequest(string? DeviceId, int? GroupNo);

public record SessionActiveRequest(string? StudentId, string? SessionId);

public record SessionEndRequest(string SessionId);

public record SessionEndResponse(string SessionId, string Status);

// Shared shape for /api/sessions/confirm and /api/sessions/active — same response either way,
// so Electron's examBridge.examConfirmed(data) works identically after Potvrdi or after a status poll.
public record SessionStateResponse(
    string SessionId,
    string Status,
    string WizardStep,
    StudentDto Student,
    ExamDto Exam,
    List<string> SelectedOutcomes,
    int? GroupNo,
    DateTime? StartedAt);
