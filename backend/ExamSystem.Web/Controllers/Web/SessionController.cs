using System.Globalization;
using ExamSystem.Web.Auth;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Models;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Web;

// The IN_EXAM / terminal part of the wizard: the during-exam instructions page (resumed straight
// here after login while a session is IN_EXAM — see ExamsController.Index) and the online
// completion page shown after "Zavrsi ispit" (Electron just quits instead, see session-instructions.js).
public class SessionController(
    SamlUserService samlUserService,
    SessionService sessions,
    InstructionService instructions) : Controller
{
    private const string ExamInstructionId = "EXAM-GENERAL-V2";

    [HttpGet("/session/instructions")]
    public async Task<IActionResult> InstructionsExam()
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.TryFindActiveByStudentAsync(student.Id);
        if (session is null)
            return Redirect("/exams");
        if (session.WizardStep != WizardStep.IN_EXAM)
            return Redirect(WizardHelpers.RouteFor(session));

        var html = await instructions.GetHtmlAsync(ExamInstructionId);
        var isEn = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var body = html is null ? string.Empty : (isEn ? html.Instructions.En : html.Instructions.Hr);
        var localDt = WizardHelpers.ToZagrebLocal(session.Exam.ExamDateTime);
        var outcomes = session.Outcomes.Select(o => o.LearningOutcome.OutcomeCode).OrderBy(c => c).ToList();

        return View(new InstructionsExamPageViewModel(session.SessionId, session.Exam.ExamId,
            session.Exam.Course.CourseNameHr, session.Exam.Course.CourseNameEn,
            localDt, session.Exam.Classroom, outcomes, body));
    }

    [HttpGet("/session/completed")]
    public IActionResult Completed() => View();
}
