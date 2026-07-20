namespace ExamSystem.Web.Domain;

public class SessionOutcome
{
    public long Id { get; set; }
    public long ExamSessionId { get; set; }
    public long LearningOutcomeId { get; set; }
}
