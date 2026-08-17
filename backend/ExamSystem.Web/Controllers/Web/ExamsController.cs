using System.Globalization;
using ExamSystem.Web.Auth;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;
using ExamSystem.Web.Models;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Web;

// The wizard's EXAMS/OUTCOMES/INSTRUCTIONS/CONFIRM steps. Every GET here re-checks the persisted
// ExamSession.WizardStep and redirects to WizardHelpers.RouteFor(session) when the URL doesn't
// match it — deep-linking forward past the persisted step is impossible by construction.
//
// All four steps run BEFORE confirmation, so the exam's course name / date / classroom are read live
// from Infoeduka on every render (the session's snapshot columns are still NULL until Potvrdi).
public class ExamsController(
    SamlUserService samlUserService,
    SessionService sessions,
    ExamQueryService examQuery,
    ExamDetailsService examDetails,
    InstructionService instructions,
    IInfoedukaClient infoeduka) : Controller
{
    private const string InitialInstructionId = "EXAM-GENERAL-V1";

    [HttpGet("/exams")]
    public async Task<IActionResult> Index()
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
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
        var existing = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (existing is not null)
            return Redirect(WizardHelpers.RouteFor(existing));

        try
        {
            await sessions.CreateDraftAsync(student.StudentId, examId);
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
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.OUTCOMES)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(await BuildOutcomesViewModel(student, session, showError: false));
    }

    [HttpPost("/exams/{examId}/outcomes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OutcomesNext(string examId, [FromForm] List<string>? outcomes)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.OUTCOMES)
            return Redirect(WizardHelpers.RouteFor(session));

        if (outcomes is null || outcomes.Count == 0)
            return View("Outcomes", await BuildOutcomesViewModel(student, session, showError: true));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.INSTRUCTIONS, outcomes);
        return Redirect($"/exams/{examId}/instructions");
    }

    [HttpGet("/exams/{examId}/instructions")]
    public async Task<IActionResult> InstructionsInitial(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(await BuildInstructionsViewModel(examId, showError: false));
    }

    [HttpPost("/exams/{examId}/instructions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InstructionsInitialNext(string examId, [FromForm] bool accepted)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
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
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.INSTRUCTIONS)
            return Redirect(WizardHelpers.RouteFor(session));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.OUTCOMES, null);
        return Redirect($"/exams/{examId}/outcomes");
    }

    [HttpGet("/exams/{examId}/confirm")]
    public async Task<IActionResult> Confirm(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.CONFIRM)
            return Redirect(WizardHelpers.RouteFor(session));

        return View(await BuildConfirmViewModel(student, session));
    }

    [HttpPost("/exams/{examId}/confirm/back")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBack(string examId)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.StudentId);
        if (session is null)
            return Redirect("/exams");
        if (session.ExamId != examId || session.WizardStep != WizardStep.CONFIRM)
            return Redirect(WizardHelpers.RouteFor(session));

        await sessions.SaveStepAsync(session.SessionId, WizardStep.INSTRUCTIONS, null);
        return Redirect($"/exams/{examId}/instructions");
    }

    private async Task<OutcomesPageViewModel> BuildOutcomesViewModel(
        InfoedukaStudent student, ExamSession session, bool showError)
    {
        var registration = await RegistrationForAsync(student.StudentId, session.ExamId);
        var details = await examDetails.GetDetailsAsync(student.StudentId, registration.CourseId);
        var selectedCodes = session.Outcomes.Select(o => o.OutcomeCode).ToHashSet();
        var cards = details.Outcomes
            .Select(o => new OutcomeCardViewModel(o, selectedCodes.Contains(o.OutcomeCode)))
            .ToList();
        return new OutcomesPageViewModel(session.ExamId, registration.CourseNameHr,
            registration.CourseNameEn, cards, showError);
    }

    private async Task<InstructionsInitialPageViewModel> BuildInstructionsViewModel(string examId, bool showError)
    {
        var html = await instructions.GetHtmlAsync(InitialInstructionId);
        var isEn = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var body = html is null ? string.Empty : (isEn ? html.Instructions.En : html.Instructions.Hr);
        return new InstructionsInitialPageViewModel(examId, body, showError);
    }

    private async Task<ConfirmPageViewModel> BuildConfirmViewModel(InfoedukaStudent student, ExamSession session)
    {
        var registration = await RegistrationForAsync(student.StudentId, session.ExamId);
        var localDt = WizardHelpers.ToZagrebLocal(registration.ExamDateTime);
        var outcomes = session.Outcomes.Select(o => o.OutcomeCode).OrderBy(c => c).ToList();
        return new ConfirmPageViewModel(session.ExamId, registration.CourseNameHr,
            registration.CourseNameEn, localDt, registration.Classroom, outcomes);
    }

    // The exam behind an unconfirmed session: it lives in Infoeduka, and the student's registration
    // list is the only place that names it.
    private async Task<InfoedukaRegistration> RegistrationForAsync(string studentId, string examId)
        => (await infoeduka.GetRegistrationsAsync(studentId)).FirstOrDefault(r => r.ExamId == examId)
           ?? throw new ExamNotFoundException(examId);
}
