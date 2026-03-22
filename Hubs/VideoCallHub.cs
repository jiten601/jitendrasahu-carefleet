using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CareFleet.Hubs
{
    public class VideoCallHub : Hub
    {
        // Map Email to ConnectionId
        private static readonly ConcurrentDictionary<string, string> UserConnections = new ConcurrentDictionary<string, string>();

        public override async Task OnConnectedAsync()
        {
            var email = Context.GetHttpContext()?.Session.GetString("UserEmail");
            if (!string.IsNullOrEmpty(email))
            {
                UserConnections[email] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var email = Context.GetHttpContext()?.Session.GetString("UserEmail");
            if (!string.IsNullOrEmpty(email))
            {
                UserConnections.TryRemove(email, out _);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task StartCall(string targetEmail, string callerName, string callerInitials)
        {
            if (UserConnections.TryGetValue(targetEmail, out var targetConnectionId))
            {
                var callerEmail = Context.GetHttpContext()?.Session.GetString("UserEmail") ?? "Unknown";
                await Clients.Client(targetConnectionId).SendAsync("IncomingCall", callerEmail, callerName, callerInitials);
            }
        }

        public async Task AcceptCall(string callerEmail)
        {
            if (UserConnections.TryGetValue(callerEmail, out var callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("CallAccepted", Context.ConnectionId);
            }
        }

        public async Task RejectCall(string callerEmail)
        {
            if (UserConnections.TryGetValue(callerEmail, out var callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("CallRejected");
            }
        }

        public async Task HangUp(string targetEmail)
        {
            if (UserConnections.TryGetValue(targetEmail, out var targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("CallEnded");
            }
        }

        public async Task SendSignal(string targetEmail, string signal)
        {
            if (UserConnections.TryGetValue(targetEmail, out var targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", signal);
            }
        }
    }
}
