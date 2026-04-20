using Microsoft.Extensions.Logging;
using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Test.Shared;

namespace Nisp.Test.Adam
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
                SenderEndpoint = ("127.0.0.1", 5001),
                ReceiverEndpoint = ("127.0.0.1", 5000),
                ImAliveConfig = new ImAliveConfig
                {
                    DelayInMilliseconds = 5000
                }
            });

            await peer.ConnectAsync();
            await peer.SendAsync(new UserMessage { Message = "Hello Eve" });

            var t1 = Task.Run(async () =>
            {
                await foreach (var message in peer.ReceiveAsync<UserMessage>())
                {
                    Console.WriteLine($"\nReceived message from Eve: {message.Message}\n");
                    await peer.SendAsync(new UserMessage { Message = "Hello Eve" });
                }
            });

            var t2 = Task.Run(async () =>
            {
                await foreach (var message in peer.ReceiveAsync<ImAliveMessage>())
                {
                    Console.WriteLine($"Eve is alive");
                }
            });

            await Task.WhenAll(t1, t2);
            await peer.CloseConnectionsAsync();
        }
    }
}