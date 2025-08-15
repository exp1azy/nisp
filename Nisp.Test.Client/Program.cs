using Nisp.Core;
using Nisp.Test.Shared;
using System.Text;
using ZLogger;

namespace Nisp.Test.Client
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 7777;

            var service = new NispService();
            service.WithLogging(builder => builder.AddZLoggerConsole());

            var client = service.CreateClient(host, port);
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
            }

            await client.StopAsync();
        }
    }
}