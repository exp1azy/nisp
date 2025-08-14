using MemoryPack;
using Nisp.Core;
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

            service.EnableLogging(builder => builder.AddZLoggerConsole());
            await service.StartClientAsync(host, port);

            for (int i = 0; i < 100; i++)
            {
                int r = Random.Shared.Next();
                string msg = $"[{i}] Hello world! Your number is {r}";

                await service.SendAsync(host, port, new UserMessage 
                { 
                    Id = i,
                    Message = msg,
                    MessageBytes = Encoding.UTF8.GetBytes(msg),
                    Time = DateTime.Now,
                    RandomNumber = r
                });
            }

            await service.StopClientAsync(host, port);
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