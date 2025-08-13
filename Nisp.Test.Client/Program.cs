using MemoryPack;
using Nisp.Core.Entities;

namespace Nisp.Test.Client
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var client = new NispProducer("localhost", 8888);
            await client.ConnectAsync(CancellationToken.None);
            Console.WriteLine("Connected");

            while (true)
            {
                await client.SendAsync(new UserMessage { Message = "Hello" }, CancellationToken.None);
                await Task.Delay(1000);
            }
        }
    }

    [MemoryPackable]
    public partial class UserMessage
    {
        public string Message { get; set; }
    }
}