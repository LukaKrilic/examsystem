namespace ExamSystem.Web.Domain;

public class SessionOutcome
{
    public long Id { get; set; }
    public long ExamSessionId { get; set; }
    public long LearningOutcomeId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;
    public LearningOutcome LearningOutcome { get; set; } = null!;
}
