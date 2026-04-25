using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

// Hub SignalR — fiecare client se conectează aici prin WebSocket
// Clientul se abonează la comentariile unui anumit user (prin userId)
public class CommentHub : Hub
{
    public async Task SubscribeToUser(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}
