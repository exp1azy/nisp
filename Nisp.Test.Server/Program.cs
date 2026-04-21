using Microsoft.Extensions.Logging;
using Nisp.Core;
using Nisp.Core.Entities;
using Nisp.Core.Messages;
using Nisp.Test.Shared;

namespace Nisp.Test.Server
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "127.0.0.1";
            const int port = 7777;

            var service = new NispService()
                .WithMessageTypes((1, typeof(UserMessage)))
                .WithCompression(b => b.UseLZ4())
                .WithLogging(builder => builder.AddConsole());

            var receiver = service.CreateReceiver(host, port);
            await receiver.ListenAsync();
            receiver.StartReceiving(ReceiveErrorBehavior.Ignore);

            var t1 = Task.Run(async () =>
            {
                await foreach (var message in receiver.ReceiveAsync<UserMessage>())
                {
                    Console.WriteLine($"Received: {message.Message}");
                }
            });

            var t2 = Task.Run(async () =>
            {
                await foreach (var message in receiver.ReceiveAsync<ImAliveMessage>())
                {
                    Console.WriteLine($"Received: {message.Id}");
                }
            });

            await Task.WhenAll(t1, t2);
            await receiver.StopAsync();
        }
    }
}