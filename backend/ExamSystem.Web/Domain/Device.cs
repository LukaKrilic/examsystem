namespace ExamSystem.Web.Domain;

public class Device
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = null!;
    public string ClientType { get; set; } = null!;      // 'electron'
    public string? Hostname { get; set; }
    public string? LocalIp { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
