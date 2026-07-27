using ExamSystem.Web.Enums;
namespace ExamSystem.Web.Domain;

public class ExamSession
{
    public long Id { get; set; }
    public string SessionId { get; set; } = null!;       // 'SESSION-001'
    public long StudentId { get; set; }
    public long ExamId { get; set; }
    public long? DeviceId { get; set; }                  // NULL for online students
    public int? GroupNo { get; set; }                    // 1 or 2, NULL for online
    public SessionStatus Status { get; set; }
    public WizardStep WizardStep { get; set; }
    public DateTimeOffset? StartedAt { get; set; }       // set at Potvrdi
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public Device? Device { get; set; }
    public List<SessionOutcome> Outcomes { get; set; } = [];
}
