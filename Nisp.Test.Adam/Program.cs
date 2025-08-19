using Nisp.Core;
using Nisp.Core.Components;
using Nisp.Test.Shared;

namespace Nisp.Test.Adam
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService();

            var peer = service.CreatePeer(new PeerConfig
            {
                ClientHost = "localhost",
                ClientPort = 5001,
                ListenerHost = "localhost",
                ListenerPort = 5000
            });

            await peer.ConnectAsync();

            await peer.SendAsync(new UserMessage { Message = "Ping" });
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Eve: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Ping" });
            }
        }
    }
}