using Microsoft.Extensions.Logging;
using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Test.Shared;

namespace Nisp.Test.Eve
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService()
                .WithMessageTypes((1, typeof(UserMessage)))
                .WithCompression(f => f.UseSnappier())
                .WithLogging(b => b.AddConsole());

            var peer = service.CreatePeer(new PeerConfig
            {
                SenderEndpoint = ("127.0.0.1", 5000),
                ReceiverEndpoint = ("127.0.0.1", 5001),
                ImAliveConfig = new ImAliveConfig
                {
                    DelayInMilliseconds = 5000
                }
            });

            await peer.ConnectAsync();

            var t1 = Task.Run(async () =>
            {
                await foreach (var message in peer.ReceiveAsync<UserMessage>())
                {
                    Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");
                    await peer.SendAsync(new UserMessage { Message = "Hello Adam" });
                    await Task.Delay(1000);
                }
            });

            var t2 = Task.Run(async () =>
            {
                await foreach (var message in peer.ReceiveAsync<ImAliveMessage>())
                {
                    Console.WriteLine($"Adam is alive");
                }
            });

            await Task.WhenAll(t1, t2);
            await peer.CloseConnectionsAsync();
        }
    }
}