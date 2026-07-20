namespace ExamSystem.Web.Domain;

public class Instruction
{
    public long Id { get; set; }
    public string InstructionId { get; set; } = null!;   // 'EXAM-GENERAL-V1'
    public int VersionNo { get; set; }
    public string HtmlHr { get; set; } = null!;
    public string HtmlEn { get; set; } = null!;
}
