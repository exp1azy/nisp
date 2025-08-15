using Nisp.Core;
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

            var peer = service.CreatePeer("localhost", 5001, "localhost", 5000);
            await peer.ConnectAsync();
            
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Pong" });
                await Task.Delay(500);
            }
        }
    }
}