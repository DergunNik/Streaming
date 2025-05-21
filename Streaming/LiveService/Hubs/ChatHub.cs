using Microsoft.AspNetCore.SignalR;

namespace LiveService.Hubs;

public class ChatHub : Hub
{
    public async Task JoinStream(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
    }

    public async Task LeaveStream(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, streamId);
    }

    public async Task SendMessage(string streamId, string user, string message)
    {
        await Clients.Group(streamId).SendAsync("ReceiveMessage", user, message);
    }
}