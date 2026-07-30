using ExamSystem.Web.Data;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Enums;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// CreateDraftAsync and SaveStepAsync back the wizard steps (exam selection, OUTCOMES/INSTRUCTIONS
// progression) called from the Phase 5 Razor MVC controllers (Controllers/Web) — there is no REST
// endpoint for them, since only the browser wizard drives step progress. LockoutAndStateMachineTests
// exercises them directly the same way those controllers do.
public class SessionService(ExamDbContext db)
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

    public async Task<ExamSession> CreateDraftAsync(long studentId, string examId)
    {
        var existing = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE);
        if (existing is not null)
        {
            var exam = await db.Exams.FirstOrDefaultAsync(e => e.ExamId == examId)
                ?? throw new ExamNotFoundException(examId);
            if (existing.ExamId == exam.Id)
                return existing;   // idempotent re-select of the same in-progress exam
            throw new InvalidStepTransitionException(
                $"Student already has an active session '{existing.SessionId}'");
        }

        var chosen = await db.Exams.FirstOrDefaultAsync(e => e.ExamId == examId)
            ?? throw new ExamNotFoundException(examId);

        // Only exams the student is actually registered for can become a session — otherwise the
        // wizard's "select" action would let a student start a session for an arbitrary exam ID.
        var registered = await db.ExamRegistrations.AnyAsync(r => r.StudentId == studentId && r.ExamId == chosen.Id);
        if (!registered)
            throw new ExamNotFoundException(examId);

        var locked = await db.LockedExams.AnyAsync(l => l.StudentId == studentId && l.ExamId == chosen.Id);
        if (locked)
            throw new ExamLockedException(examId);

        var session = new ExamSession
        {
            SessionId = $"SESSION-{Guid.NewGuid():N}",
            StudentId = studentId,
            ExamId = chosen.Id,
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
            .Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(sessionId);

        if (!LegalStepMoves.Contains((session.WizardStep, to)))
            throw new InvalidStepTransitionException(
                $"Cannot move session '{sessionId}' from '{session.WizardStep}' to '{to}'");

        if (session.WizardStep == WizardStep.OUTCOMES && to == WizardStep.INSTRUCTIONS)
        {
            if (outcomeCodes is null || outcomeCodes.Count == 0)
                throw new InvalidStepTransitionException("At least one outcome must be selected");

            var outcomes = await db.LearningOutcomes
                .Where(o => o.CourseId == session.Exam.CourseId && outcomeCodes.Contains(o.OutcomeCode))
                .ToListAsync();
            db.SessionOutcomes.RemoveRange(session.Outcomes);
            foreach (var o in outcomes)
                session.Outcomes.Add(new SessionOutcome { LearningOutcomeId = o.Id });
        }

        session.WizardStep = to;
        await db.SaveChangesAsync();
        return session;
    }

    public async Task<ExamSession> ConfirmAsync(long studentId, string? deviceId, int? groupNo)
    {
        var session = await db.ExamSessions
            .Include(s => s.Student)
            .Include(s => s.Outcomes).ThenInclude(o => o.LearningOutcome)
            .Include(s => s.Exam).ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(studentId.ToString());

        if (session.WizardStep == WizardStep.IN_EXAM)
            throw new SessionAlreadyConfirmedException(session.SessionId);
        if (session.WizardStep != WizardStep.CONFIRM)
            throw new InvalidStepTransitionException(
                $"Cannot confirm session '{session.SessionId}' from step '{session.WizardStep}'");

        var chosen = session.Exam;

        // Lock every OTHER registered exam at the same date/time — forever. One SaveChangesAsync
        // covers both the lockout inserts and the session activation; the LockedExam unique
        // constraint makes a concurrent double-confirm's second insert fail and roll back cleanly.
        var sameTerm = await db.ExamRegistrations
            .Where(r => r.StudentId == studentId)
            .Select(r => r.Exam)
            .Where(e => e.ExamDateTime == chosen.ExamDateTime && e.Id != chosen.Id)
            .ToListAsync();
        foreach (var e in sameTerm)
            db.LockedExams.Add(new LockedExam
            {
                StudentId = studentId,
                ExamId = e.Id,
                Reason = $"SAME_TERM_AS_{chosen.ExamId}",
                CreatedAt = DateTimeOffset.UtcNow
            });

        session.StartedAt = DateTimeOffset.UtcNow;
        session.WizardStep = WizardStep.IN_EXAM;

        if (deviceId is not null)
        {
            var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            session.DeviceId = device?.Id;
        }
        session.GroupNo = groupNo;

        await db.SaveChangesAsync();
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

    public async Task<ExamSession> FindActiveByStudentAsync(long studentId)
    {
        return await LoadedSessions()
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE)
            ?? throw new NoActiveSessionException(studentId.ToString());
    }

    // Non-throwing variant for the Razor wizard controllers, which need to branch on "no active
    // session" (render the exam list / redirect to it) rather than treat it as an error.
    public async Task<ExamSession?> TryFindActiveByStudentAsync(long studentId)
    {
        return await LoadedSessions()
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SessionStatus.ACTIVE);
    }

    public async Task<ExamSession> FindActiveByStudentIdAsync(string studentId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId)
            ?? throw new StudentNotFoundException(studentId);
        return await FindActiveByStudentAsync(student.Id);
    }

    public async Task<ExamSession> FindBySessionIdAsync(string sessionId)
    {
        return await LoadedSessions().FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new NoActiveSessionException(sessionId);
    }

    private IQueryable<ExamSession> LoadedSessions() => db.ExamSessions
        .Include(s => s.Student)
        .Include(s => s.Exam).ThenInclude(e => e.Course)
        .Include(s => s.Outcomes).ThenInclude(o => o.LearningOutcome);

    public static SessionStateResponse ToStateResponse(ExamSession session)
    {
        var e = session.Exam;
        return new SessionStateResponse(
            session.SessionId,
            session.Status.ToString(),
            session.WizardStep.ToString(),
            new StudentDto(session.Student.StudentId, session.Student.FullName, session.Student.Jmbag),
            new ExamDto(e.ExamId, e.Course.CourseNameHr, e.Course.CourseNameEn, e.Course.CourseId,
                e.ExamDateTime.UtcDateTime, e.Classroom),
            session.Outcomes.Select(o => o.LearningOutcome.OutcomeCode).ToList(),
            session.GroupNo,
            session.StartedAt?.UtcDateTime);
    }
}
