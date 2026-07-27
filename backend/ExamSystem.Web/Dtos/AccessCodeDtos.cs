namespace ExamSystem.Web.Dtos;

public record AccessCodesRequest(string ExamId);

public record AccessCodesResponse(string ExamId, AccessCodesDto AccessCodes);

public record AccessCodesDto(string Group1, string Group2);
