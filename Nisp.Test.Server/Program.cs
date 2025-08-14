using MemoryPack;
using Nisp.Core;
using ZLogger;

namespace Nisp.Test.Server
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 7777;

            var service = new NispService();

            await service.StartListenerAsync(host, port);

            await foreach (var message in service.ReceiveAsync<UserMessage>(host, port))
            {
                Console.WriteLine(message.Message);
            }

            await service.StopListenerAsync(host, port);
        }
    }

    [MemoryPackable]
    public partial class UserMessage
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public byte[] MessageBytes { get; set; }
        public DateTime Time { get; set; }
        public int RandomNumber { get; set; }
    }
}