using Microsoft.Extensions.Logging;
using Nisp.Core;
using Nisp.Core.Messages;
using Nisp.Test.Shared;
using System.Text;

namespace Nisp.Test.Client
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "127.0.0.1";
            const int port = 7777;

            var service = new NispService()
                .WithMessageTypes((1, typeof(UserMessage)))
                .WithLogging(builder => builder.AddConsole())
                .WithCompression(b => b.UseLZ4());

            var client = service.CreateSender(host, port);
            await client.ConnectAsync();

            for (int i = 0; i < 20; i++)
            {
                string message = $"Hello from client {i}";

                await client.SendAsync(new UserMessage
                {
                    Id = i,
                    Message = message,
                    MessageBytes = Encoding.UTF8.GetBytes(message),
                    RandomNumber = Random.Shared.Next(),
                    Time = DateTime.Now
                });

                await client.SendAsync(new ImAliveMessage
                {
                    Id = Guid.NewGuid(),
                    DateTime = DateTime.Now
                });
            }

            await client.StopAsync();
        }
    }
}