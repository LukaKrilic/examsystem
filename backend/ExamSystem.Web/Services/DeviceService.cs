using ExamSystem.Web.Data;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class DeviceService(ExamDbContext db)
{
    public async Task<DeviceRegisterResponse> RegisterAsync(DeviceRegisterRequest request)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId);
        if (device is null)
        {
            device = new Device { DeviceId = request.DeviceId };
            db.Devices.Add(device);
        }
        device.ClientType = request.ClientType;
        device.Hostname = request.Hostname;
        device.LocalIp = request.LocalIp;
        device.LastSeen = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return new DeviceRegisterResponse(device.DeviceId, true);
    }
}
