using ExamSystem.Web.Dtos;

namespace ExamSystem.Web.Models;

public record OutcomeCardViewModel(OutcomeDto Outcome, bool Selected);

public record OutcomesPageViewModel(
    string ExamId,
    string CourseNameHr,
    string CourseNameEn,
    List<OutcomeCardViewModel> Outcomes,
    bool ShowSelectionError);

public record InstructionsInitialPageViewModel(
    string ExamId,
    string InstructionsHtml,
    bool ShowAcceptError);

public record ConfirmPageViewModel(
    string ExamId,
    string CourseNameHr,
    string CourseNameEn,
    DateTime ExamDateTimeLocal,
    string Classroom,
    List<string> SelectedOutcomes);

public record InstructionsExamPageViewModel(
    string SessionId,
    string ExamId,
    string CourseNameHr,
    string CourseNameEn,
    DateTime ExamDateTimeLocal,
    string Classroom,
    List<string> SelectedOutcomes,
    string InstructionsHtml);
