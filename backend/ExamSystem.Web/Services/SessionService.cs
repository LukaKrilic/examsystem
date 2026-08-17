using ExamSystem.Web.Data;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// CreateDraftAsync and SaveStepAsync back the wizard steps (exam selection, OUTCOMES/INSTRUCTIONS
// progression) called from the Phase 5 Razor MVC controllers (Controllers/Web) — there is no REST
// endpoint for them, since only the browser wizard drives step progress. LockoutAndStateMachineTests
// exercises them directly the same way those controllers do.
//
// Infoeduka is consulted only BEFORE confirmation: to check the student is registered for the exam
// they picked, to validate the outcome codes they selected, and once more at Potvrdi to take the
// snapshot. After that the session is self-contained — see ConfirmAsync.
public class SessionService(ExamDbContext db, IInfoedukaClient infoeduka)
{
    // Legal Natrag/Dalje moves per the CLAUDE.md wizard state-machine table. CONFIRM -> IN_EXAM is
    // deliberately excluded here — that transition only happens through ConfirmAsync, which also
    // writes the same-term lockout in the same SaveChangesAsync.
    private static readonly (WizardStep From, WizardStep To)[] LegalStepMoves =
    [
        (WizardStep.OUTCOMES, WizardStep.INSTRUCTIONS),
        (WizardStep.INSTRUCTIONS, WizardStep.CONFIRM),
        (WizardStep.INSTRUCTIONS, WizardStep.OUTCOMES),
        (WizardStep.CONFIRM, WizardStep.INSTRUCTIONS),
    ];

    public async Task<ExamSession> CreateDraftAsync(string studentId, string examId)
    {
        var existing = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE);
        if (existing is not null)
        {
            if (existing.ExamId == examId)
                return existing;   // idempotent re-select of the same in-progress exam
            throw new InvalidStepTransitionException(
                $"Student already has an active session '{existing.SessionId}'");
        }

        // Only exams the student is actually registered for can become a session — otherwise the
        // wizard's "select" action would let a student start a session for an arbitrary exam ID.
        // The registration list is Infoeduka's; we hold no copy of it.
        var registrations = await infoeduka.GetRegistrationsAsync(studentId);
        if (registrations.All(r => r.ExamId != examId))
            throw new ExamNotFoundException(examId);

        var locked = await db.LockedExams.AnyAsync(l => l.StudentId == studentId && l.ExamId == examId);
        if (locked)
            throw new ExamLockedException(examId);

        var session = new ExamSession
        {
            SessionId = $"SESSION-{Guid.NewGuid():N}",
            StudentId = studentId,
            ExamId = examId,
            Status = SessionStatus.ACTIVE,
            WizardStep = WizardStep.OUTCOMES,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task<ExamSession> SaveStepAsync(string sessionId, WizardStep to, List<string>? outcomeCodes)
    {
        var session = await db.ExamSessions
            .Include(s => s.Outcomes)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(sessionId);

        if (!LegalStepMoves.Contains((session.WizardStep, to)))
            throw new InvalidStepTransitionException(
                $"Cannot move session '{sessionId}' from '{session.WizardStep}' to '{to}'");

        if (session.WizardStep == WizardStep.OUTCOMES && to == WizardStep.INSTRUCTIONS)
        {
            if (outcomeCodes is null || outcomeCodes.Count == 0)
                throw new InvalidStepTransitionException("At least one outcome must be selected");

            // The posted codes are still validated against the course's real outcomes — the list just
            // comes from Infoeduka now instead of a local table. Never trust the browser's values.
            var valid = await ValidOutcomeCodesAsync(session.StudentId, session.ExamId);
            var accepted = outcomeCodes.Where(valid.Contains).Distinct().ToList();
            if (accepted.Count == 0)
                throw new InvalidStepTransitionException("At least one outcome must be selected");

            db.SessionOutcomes.RemoveRange(session.Outcomes);
            foreach (var code in accepted)
                session.Outcomes.Add(new SessionOutcome { OutcomeCode = code });
        }

        session.WizardStep = to;
        await db.SaveChangesAsync();
        return session;
    }

    public async Task<ExamSession> ConfirmAsync(string studentId, string? deviceId, int? groupNo)
    {
        var session = await db.ExamSessions
            .Include(s => s.Outcomes)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(studentId);

        if (session.WizardStep == WizardStep.IN_EXAM)
            throw new SessionAlreadyConfirmedException(session.SessionId);
        if (session.WizardStep != WizardStep.CONFIRM)
            throw new InvalidStepTransitionException(
                $"Cannot confirm session '{session.SessionId}' from step '{session.WizardStep}'");

        // The frozen-at-confirm data, fetched once — the last time this session ever asks Infoeduka
        // anything. The registration list is deliberately UNFILTERED: a same-term exam 31 minutes away
        // is outside the ±30 min window but must still be locked.
        var student = await infoeduka.GetStudentAsync(studentId)
            ?? throw new StudentNotFoundException(studentId);
        var allRegistrations = await infoeduka.GetRegistrationsAsync(studentId);
        var chosen = allRegistrations.FirstOrDefault(r => r.ExamId == session.ExamId)
            ?? throw new ExamNotFoundException(session.ExamId);

        // 1. lock every OTHER registered exam at the same date/time — forever
        var sameTerm = allRegistrations.Where(r => r.ExamDateTime == chosen.ExamDateTime && r.ExamId != chosen.ExamId);
        foreach (var r in sameTerm)
            db.LockedExams.Add(new LockedExam
            {
                StudentId = studentId,
                ExamId = r.ExamId,
                Reason = $"SAME_TERM_AS_{chosen.ExamId}",
                CreatedAt = DateTimeOffset.UtcNow
            });

        // 2. activate the session + snapshot the frozen data onto the row itself
        session.StartedAt = DateTimeOffset.UtcNow;
        session.WizardStep = WizardStep.IN_EXAM;
        session.CourseNameHr = chosen.CourseNameHr;
        session.CourseNameEn = chosen.CourseNameEn;
        session.ExamDateTime = chosen.ExamDateTime;
        session.Classroom = chosen.Classroom;
        session.StudentFullName = student.FullName;
        session.StudentJmbag = student.Jmbag;

        if (deviceId is not null)
        {
            var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            session.DeviceId = device?.Id;
        }
        session.GroupNo = groupNo;

        await db.SaveChangesAsync();   // ONE transaction: locks + activation, all-or-nothing
        return session;
    }

    public async Task<ExamSession> EndAsync(string sessionId)
    {
        var session = await db.ExamSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new NoActiveSessionException(sessionId);

        if (session.Status == SessionStatus.ACTIVE)
        {
            session.Status = SessionStatus.FINISHED;
            session.EndedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        return session;
    }

    public async Task<ExamSession> FindActiveByStudentAsync(string studentId)
    {
        return await LoadedSessions()
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(studentId);
    }

    // Non-throwing variant for the Razor wizard controllers, which need to branch on "no active
    // session" (render the exam list / redirect to it) rather than treat it as an error.
    public async Task<ExamSession?> TryFindActiveByStudentAsync(string studentId)
    {
        return await LoadedSessions()
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE);
    }

    public async Task<ExamSession> FindBySessionIdAsync(string sessionId)
    {
        return await LoadedSessions().FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new NoActiveSessionException(sessionId);
    }

    // The course's real outcome codes, per Infoeduka. Needs the course id, which only the student's
    // registration for this exam knows.
    private async Task<HashSet<string>> ValidOutcomeCodesAsync(string studentId, string examId)
    {
        var registration = (await infoeduka.GetRegistrationsAsync(studentId))
            .FirstOrDefault(r => r.ExamId == examId)
            ?? throw new ExamNotFoundException(examId);

        var course = await infoeduka.GetCourseOutcomesAsync(studentId, registration.CourseId)
            ?? throw new ExamNotFoundException(registration.CourseId);

        return course.Outcomes.Select(o => o.OutcomeCode).ToHashSet();
    }

    private IQueryable<ExamSession> LoadedSessions() => db.ExamSessions.Include(s => s.Outcomes);

    // Reads the snapshot columns, never Infoeduka: every caller of this (confirm's response, the
    // resume lookup, Electron's 60 s status poll) is dealing with a session at or past Potvrdi.
    // A session still mid-wizard has no snapshot yet, hence the empty-string fallbacks.
    public static SessionStateResponse ToStateResponse(ExamSession session)
    {
        return new SessionStateResponse(
            session.SessionId,
            session.Status.ToString(),
            session.WizardStep.ToString(),
            new StudentDto(session.StudentId, session.StudentFullName ?? "", session.StudentJmbag ?? ""),
            new ExamDto(session.ExamId, session.CourseNameHr ?? "", session.CourseNameEn ?? "", "",
                session.ExamDateTime?.UtcDateTime ?? default, session.Classroom ?? ""),
            session.Outcomes.Select(o => o.OutcomeCode).OrderBy(c => c).ToList(),
            session.GroupNo,
            session.StartedAt?.UtcDateTime);
    }
}
