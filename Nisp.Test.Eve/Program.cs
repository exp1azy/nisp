using Microsoft.Extensions.Logging;
using Nisp.Core;
using Nisp.Core.Components;
using Nisp.Test.Shared;

namespace Nisp.Test.Eve
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService()
                .WithCompression()
                .WithLogging(builder => builder.AddConsole());

            var peer = service.CreateBidirectional(new PeerConfig
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