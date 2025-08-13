using MemoryPack;
using Nisp.Core.Entities;

namespace Nisp.Test.Server
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var server = new NispConsumer("localhost", 8888);
            await server.ConnectAsync(CancellationToken.None);
            Console.WriteLine("Server listening.");

            while (true)
            {
                var message = await server.ReceiveAsync<UserMessage>(CancellationToken.None);
                Console.WriteLine($"Received: {message.Message}");
            }
        }
    }

    [MemoryPackable]
    public partial class UserMessage
    {
        public string Message { get; set; }
    }
}