using Nisp.Core;
using Nisp.Test.Shared;

namespace Nisp.Test.Server
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 7777;

            var service = new NispService();
            var listener = service.CreateListener(host, port);
            await listener.ListenAsync();

            await foreach (var message in listener.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"Received: {message.Message}");
            }

            await listener.StopAsync();
        }
    }
}