using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Test.Shared;
using System.Security.Cryptography.X509Certificates;
using ZLogger;

namespace Nisp.Test.Adam
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

            var peer = service.CreatePeer("localhost", 5000, "localhost", 5001);
            await peer.ConnectAsync(1000);

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