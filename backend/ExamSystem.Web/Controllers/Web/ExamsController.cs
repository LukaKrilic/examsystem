using System.Globalization;
using ExamSystem.Web.Auth;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Models;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Web;

// The wizard's EXAMS/OUTCOMES/INSTRUCTIONS/CONFIRM steps. Every GET here re-checks the persisted
// ExamSession.WizardStep and redirects to WizardHelpers.RouteFor(session) when the URL doesn't
// match it — deep-linking forward past the persisted step is impossible by construction.
public class ExamsController(
    SamlUserService samlUserService,
    SessionService sessions,
    ExamQueryService examQuery,
    ExamDetailsService examDetails,
    InstructionService instructions) : Controller
{
    private const string InitialInstructionId = "EXAM-GENERAL-V1";

    [HttpGet("/exams")]
    public async Task<IActionResult> Index()
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is not null)
            return Redirect(WizardHelpers.RouteFor(session));

        var response = await examQuery.ActiveExamsAsync(student.StudentId, WizardHelpers.NowZagreb());
        return View(response);
    }

    [HttpPost("/exams/{examId}/select")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var existing = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (existing is not null)
            return Redirect(WizardHelpers.RouteFor(existing));

        try
        {
            await sessions.CreateDraftAsync(student.Id, examId);
        }
        catch (ExamNotFoundException)
        {
            return Redirect("/exams");
        }
        catch (ExamLockedException)
        {
            return Redirect("/exams");
        }

        return Redirect($"/exams/{examId}/outcomes");
    }

    [HttpGet("/exams/{examId}/outcomes")]
    public async Task<IActionResult> Outcomes(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.OUTCOMES)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(await BuildOutcomesViewModel(student, session, showError: false));
    }

    [HttpPost("/exams/{examId}/outcomes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OutcomesNext(string examId, [FromForm] List<string>? outcomes)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.OUTCOMES)
            return Redirect(WizardHelpers.RouteFor(session));

        if (outcomes is null || outcomes.Count == 0)
            return View("Outcomes", await BuildOutcomesViewModel(student, session, showError: true));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.INSTRUCTIONS, outcomes);
        return Redirect($"/exams/{examId}/instructions");
    }

    [HttpGet("/exams/{examId}/instructions")]
    public async Task<IActionResult> InstructionsInitial(string examId)
    {
        var session = await sessions.TryFindActiveByStudentAsync(
            (await samlUserService.ResolveStudentAsync(User)).Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(await BuildInstructionsViewModel(examId, showError: false));
    }

    [HttpPost("/exams/{examId}/instructions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InstructionsInitialNext(string examId, [FromForm] bool accepted)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
            return Redirect(WizardHelpers.RouteFor(session));

        if (!accepted)
            return View("InstructionsInitial", await BuildInstructionsViewModel(examId, showError: true));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.CONFIRM, null);
        return Redirect($"/exams/{examId}/confirm");
    }

    [HttpPost("/exams/{examId}/instructions/back")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InstructionsInitialBack(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
            return Redirect(WizardHelpers.RouteFor(session));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.OUTCOMES, null);
        return Redirect($"/exams/{examId}/outcomes");
    }

    [HttpGet("/exams/{examId}/confirm")]
    public async Task<IActionResult> Confirm(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.CONFIRM)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(BuildConfirmViewModel(session));
    }

    [HttpPost("/exams/{examId}/confirm/back")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBack(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.Exam.ExamId != examId || session.WizardStep != WizardStep.CONFIRM)
            return Redirect(WizardHelpers.RouteFor(session));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.INSTRUCTIONS, null);
        return Redirect($"/exams/{examId}/instructions");
    }

    private async Task<OutcomesPageViewModel> BuildOutcomesViewModel(Student student, ExamSession session, bool showError)
    {
        var details = await examDetails.GetDetailsAsync(student.StudentId, session.Exam.Course.CourseId);
        var selectedCodes = session.Outcomes.Select(o => o.LearningOutcome.OutcomeCode).ToHashSet();
        var cards = details.Outcomes
            .Select(o => new OutcomeCardViewModel(o, selectedCodes.Contains(o.OutcomeCode)))
            .ToList();
        return new OutcomesPageViewModel(session.Exam.ExamId, session.Exam.Course.CourseNameHr,
            session.Exam.Course.CourseNameEn, cards, showError);
    }

    private async Task<InstructionsInitialPageViewModel> BuildInstructionsViewModel(string examId, bool showError)
    {
        var html = await instructions.GetHtmlAsync(InitialInstructionId);
        var isEn = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var body = html is null ? string.Empty : (isEn ? html.Instructions.En : html.Instructions.Hr);
        return new InstructionsInitialPageViewModel(examId, body, showError);
    }

    private static ConfirmPageViewModel BuildConfirmViewModel(ExamSession session)
    {
        var localDt = WizardHelpers.ToZagrebLocal(session.Exam.ExamDateTime);
        var outcomes = session.Outcomes.Select(o => o.LearningOutcome.OutcomeCode).OrderBy(c => c).ToList();
        return new ConfirmPageViewModel(session.Exam.ExamId, session.Exam.Course.CourseNameHr,
            session.Exam.Course.CourseNameEn, localDt, session.Exam.Classroom, outcomes);
    }
}
