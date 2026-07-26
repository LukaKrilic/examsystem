namespace ExamSystem.Web.Dtos;

public record InstructionsHtmlRequest(string InstructionId);

public record InstructionsHtmlResponse(string InstructionId, int Version, InstructionsDto Instructions);

public record InstructionsDto(string Hr, string En);
