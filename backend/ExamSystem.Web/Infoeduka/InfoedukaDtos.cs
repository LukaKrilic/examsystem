namespace ExamSystem.Web.Infoeduka;

// The shapes MockInfoeduka.Api returns — i.e. what the university's Infoeduka system owns. These are
// deliberately separate from the DTOs in Dtos/, which are OUR public API's contract with the spec:
// Infoeduka is free to change its field names without dragging our published JSON along with it.
public record InfoedukaStudent(string StudentId, string FullName, string Jmbag);

public record InfoedukaRegistration(
    string ExamId,
    string CourseId,
    string CourseNameHr,
    string CourseNameEn,
    DateTimeOffset ExamDateTime,
    string Classroom);

public record InfoedukaOutcome(
    string OutcomeCode,
    decimal TotalPointsEarned,
    decimal TotalPointsMax,
    decimal ExamPointsEarned,
    decimal ExamPointsMax);

public record InfoedukaCourseOutcomes(string CourseId, string CourseName, IReadOnlyList<InfoedukaOutcome> Outcomes);
