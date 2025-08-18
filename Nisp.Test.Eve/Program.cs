using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Test.Shared;
using ZLogger;

namespace Nisp.Test.Eve
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService()
                .WithLogging(builder => builder.AddZLoggerConsole())
                .WithCompression();

            var peer = service.CreatePeer(new PeerConfig
            {
                ClientHost = "localhost",
                ClientPort = 5000,
                ListenerHost = "localhost",
                ListenerPort = 5001
            });

            await peer.ConnectAsync();
            
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Pong" });
            }
        }
    }
}