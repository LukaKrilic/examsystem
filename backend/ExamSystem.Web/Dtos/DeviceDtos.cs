namespace ExamSystem.Web.Dtos;

public record DeviceRegisterRequest(string ClientType, string DeviceId, string? Hostname, string? LocalIp);

public record DeviceRegisterResponse(string DeviceId, bool Registered);
