using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Test.Shared;
using System.Security.Cryptography.X509Certificates;
using ZLogger;

namespace Nisp.Test.Eve
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var cert = new X509Certificate2("D:\\projects\\Nisp.Core\\server.pfx", "1234");

            var service = new NispService()
                .WithLogging(builder => builder.AddZLoggerConsole())
                .WithCompression()
                .WithSsl(new SslOptions { Certificate = cert });

            var peer = service.CreatePeer("localhost", 5001, "localhost", 5000);
            await peer.ConnectAsync(1000);
            
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Pong" });
                await Task.Delay(500);
            }
        }
    }
}