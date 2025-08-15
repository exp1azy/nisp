using Nisp.Core;
using Nisp.Test.Shared;
using ZLogger;

namespace Nisp.Test.Server
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 7777;

            var service = new NispService()
                .WithLogging(builder => builder.AddZLoggerConsole())
                .WithCompression();

            var listener = service.CreateListener(host, port);
            await listener.ListenAsync();

            await foreach (var _ in listener.ReceiveAsync<UserMessage>())
            {
            }

            await listener.StopAsync();
        }
    }
}