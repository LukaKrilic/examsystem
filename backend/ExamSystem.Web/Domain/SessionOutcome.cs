namespace ExamSystem.Web.Domain;

public class SessionOutcome
{
    public long Id { get; set; }
    public long ExamSessionId { get; set; }
    public string OutcomeCode { get; set; } = null!;     // external outcome code, e.g. 'I1' — no FK

    public ExamSession ExamSession { get; set; } = null!;
}
