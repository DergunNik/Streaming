using LiveService.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace LiveService.Hubs;

public interface IChatClient
{
    Task JoinedSuccessfully(string streamId, string welcomeMessage);
    Task ReceiveStreamChatMessage(string streamId, string userId, string userName, string message, DateTime timestamp);
    Task ReceiveError(string errorMessage);
}

public class ChatHub : Hub<IChatClient>
{
    private const string StreamChatGroupPrefix = "stream-chat-";
    private readonly ContentRestrictions _contentRestrictions;

    public ChatHub(IOptions<ContentRestrictions> contentRestrictions)
    {
        _contentRestrictions = contentRestrictions.Value;
    }

    public async Task JoinStreamChat(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId)) return;

        var (userId, userName) = GetAuthenticatedUserInfo();
        var groupName = GetStreamChatGroupName(streamId);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Client(Context.ConnectionId).JoinedSuccessfully(streamId,
            $"Welcome, {userName}! You've joined chat for stream '{streamId}'.");
    }

    [Authorize]
    public async Task SendMessageToStreamChat(string streamId, string message)
    {
        if (string.IsNullOrWhiteSpace(streamId) || string.IsNullOrWhiteSpace(message)) return;

        if (message.Length > _contentRestrictions.MaxCharMessageSize)
        {
            await Clients.Client(Context.ConnectionId)
                .ReceiveError(
                    $"Your message can't be longer than {_contentRestrictions.MaxCharMessageSize} characters.");
            return;
        }

        var (userId, userName) = GetAuthenticatedUserInfo();
        var timestamp = DateTime.UtcNow;
        var groupName = GetStreamChatGroupName(streamId);

        await Clients.Group(groupName).ReceiveStreamChatMessage(streamId, userId, userName, message, timestamp);
    }

    public async Task LeaveStreamChat(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId)) return;

        var groupName = GetStreamChatGroupName(streamId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private static string GetStreamChatGroupName(string streamId)
    {
        return $"{StreamChatGroupPrefix}{streamId}";
    }

    private (string UserId, string UserName) GetAuthenticatedUserInfo()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = Context.UserIdentifier ?? $"AuthUser_NoId_{Context.ConnectionId[..5]}";
            var userName = Context.User?.Identity?.Name ?? $"UnknownUser_{Context.ConnectionId[..5]}";

            return (userId, userName);
        }

        var tempUserId = Context.ConnectionId;
        var tempUserName = $"Guest_{Context.ConnectionId[..5]}";
        return (tempUserId, tempUserName);
    }
}