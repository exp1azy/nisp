using Nisp.Core;
using Nisp.Test.Shared;
using ZLogger;

namespace Nisp.Test.Adam
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService()
                .WithLogging(builder => builder.AddZLoggerConsole())
                .WithCompression();

            var peer = service.CreatePeer("localhost", 5000, "localhost", 5001);
            await peer.ConnectAsync();

            await peer.SendAsync(new UserMessage { Message = "Ping" });
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Eve: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Ping" });
                await Task.Delay(500);
            }
        }
    }
}