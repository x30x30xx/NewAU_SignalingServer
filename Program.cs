using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder();
var app = builder.Build();

var rooms = new ConcurrentDictionary<string,(string AddressJson, DateTime CreatedAt)>();

_ = Task.Run(async () => {
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        var now = DateTime.UtcNow;
        var expiredRooms = rooms
            .Where(kv => (now - kv.Value.CreatedAt).TotalMinutes > 5)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var roomId in expiredRooms)
        {
            rooms.TryRemove(roomId,out _);
        }
    }
});

string GenerateRoomId()
{
    var random = new Random();
    return random.Next(1000000,9999999).ToString();
}

app.MapPost("/create-room",async (HttpContext context) =>
{
    var request = await context.Request.ReadFromJsonAsync<CreateRoomRequest>();
    if (request == null || string.IsNullOrEmpty(request.PublicIP) || string.IsNullOrEmpty(request.LocalIP))
    {
        return Results.BadRequest("请提供公网IP、端口和本地IP、端口");
    }
    
    string roomID = GenerateRoomId();
    var addressInfo = new {PublicIP = request.PublicIP,PublicPort = request.PublicPort,LocalIP = request.LocalIP,LocalPort = request.LocalPort};
    string addressJson = JsonSerializer.Serialize(addressInfo);
    var createdAt = DateTime.UtcNow;

    while (!rooms.TryAdd(roomID,(addressJson,createdAt)))
    {
        roomID = GenerateRoomId();
    }

    return Results.Ok(new {roomID = roomID});
});

app.MapDelete("/clear-room",(string roomID) =>
{
    bool removed = rooms.TryRemove(roomID,out _);
    if (removed)
    {
        return Results.Ok(new { message = $"房间 {roomID} 已关闭" });
    }
    return Results.NotFound(new { errer = "房间不存在或已关闭" });
});

app.MapGet("/join-room",(string roomID) =>
{
    if (rooms.TryGetValue(roomID,out var entry))
    {
        var addressInfo = JsonSerializer.Deserialize<CreateRoomRequest>(entry.AddressJson);
        return Results.Ok(addressInfo);
    }
    return Results.NotFound(new { errer = "房间不存在或已关闭" });
});

app.Run();

public class CreateRoomRequest
{
    public string? PublicIP { get; set; }
    public int PublicPort { get; set; }
    public string? LocalIP { get; set; }
    public int LocalPort { get; set; }
}