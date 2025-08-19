# NISP (.NET Inter-Service Protocol)
**NISP is a high-performance communication protocol designed specifically for .NET inter-service communication.** It provides fast, efficient serialization and transmission of .NET objects between services with minimal overhead.
## Key Features
- Fast serialization using **MemoryPack**
- **LZ4 compression** for reduced network traffic
- **TLS 1.3** support for secure communication
- Logging via **ZLogger**
- **Full-duplex communication** through the peer implementation
- **Modern async API** with cancellation support
- **TCP-based** reliable transmission
## Quick Start
#### Client
Create a console application that will be a NISP **client** that sends fire-and-forget messages
```csharp
using Nisp.Core;
using Nisp.Test.Shared;

namespace Nisp.Test.Client
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 7777;

            var service = new NispService(); // create service
            var client = service.CreateClient(host, port); // create client
            await client.ConnectAsync(); // connect

            for (int i = 0; i < 20; i++)
            {
				//send message
                await client.SendAsync(new UserMessage 
                { 
	                Message = $"Hello from client {i}"
				});
            }

            await client.StopAsync();
        }
    }
}
```
#### Listener
Create a console application that will be a NISP **listener** that accepts messages from the client
```csharp
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

            var service = new NispService(); // create service
            var listener = service.CreateListener(host, port); // create listener
            await listener.ListenAsync(); // connect

			// receive messages
            await foreach (var message in listener.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"Received: {message.Message}");
            }

            await listener.StopAsync();
        }
    }
}
```
### Peer
Create a console application that will be a NISP peer that initializes a client to send a message to a remote node, as well as a server to receive messages from a remote node.
#### Nisp.Test.Adam
```csharp
using Nisp.Core;
using Nisp.Core.Components;
using Nisp.Test.Shared;

namespace Nisp.Test.Adam
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService(); // create service

			// create peer for Adam
            var peer = service.CreatePeer(new PeerConfig
            {
                ClientHost = "localhost",
                ClientPort = 5001,
                ListenerHost = "localhost",
                ListenerPort = 5000
            });

            await peer.ConnectAsync(); // connect

			// send messages to Eve
            await peer.SendAsync(new UserMessage { Message = "Ping" });

			// receive messages from Eve
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Eve: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Ping" });
            }
        }
    }
}
```
#### Nisp.Test.Eve
```csharp
using Nisp.Core;
using Nisp.Core.Components;
using Nisp.Test.Shared;

namespace Nisp.Test.Eve
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var service = new NispService(); // create service

			// create peer for Eve
            var peer = service.CreatePeer(new PeerConfig
            {
                ClientHost = "localhost",
                ClientPort = 5000,
                ListenerHost = "localhost",
                ListenerPort = 5001
            });

            await peer.ConnectAsync(); // connect

			// receive messages from Adam
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");

				// send message to Adam
                await peer.SendAsync(new UserMessage { Message = "Pong" });
            }
        }
    }
}
```
### Set Up The Service
Customize the service according to your needs. The settings apply to all the components that you use
```csharp
var service = new NispService()
    .WithCompression() // add LZ4 compression
    .WithLogging(b => b.AddZLoggerConsole()); // add ZLogger logging
```
## Components
### 1. `NispClient`
The client component for sending messages to a NISP server
```csharp
// create a client
var client = new NispClient("localhost", 5000, 
    compressionEnabled: true, 
    encryptionOptions: sslOptions,
    logger: logger);

// connect
await client.ConnectAsync();

// send a message
await client.SendAsync(new MyMessage { ... });
```
### 2. `NispListener`
The server component for receiving messages
```csharp
// create a listener
var listener = new NispListener("localhost", 5000, 
    compressionEnabled: true, 
    encryptionOptions: sslOptions,
    logger: logger);

// start listening
await listener.ListenAsync();

// receive messages
await foreach (var message in listener.ReceiveAsync<MyMessage>())
{
    // process message
}
```
### 3. `NispPeer`
A bidirectional communication peer combining both client and listener capabilities
```csharp
// create a peer
var peer = new NispPeer(
    new NispClient("peer2", 5001),
    new NispListener("peer1", 5000)
);

// connect both directions
await peer.ConnectAsync();

// send messages
await peer.SendAsync(new MyMessage { ... });

// receive messages
await foreach (var message in peer.ReceiveAsync<MyMessage>())
{
    // process message
}
```
### 4. `NispService`
A factory for creating NISP components with consistent configuration
```csharp
var service = new NispService()
    .WithLogging(builder => builder.AddZLoggerConsole())
    .WithCompression();

var client = service.CreateClient("localhost", 5000);
var listener = service.CreateListener("localhost", 5001);
var peer = service.CreatePeer(new PeerConfig { ... });
```
## Encryption
Configure TLS 1.3 encryption using `SslOptions`
```csharp
var sslOptions = new SslOptions
{
    Certificate = new X509Certificate2("cert.pfx", "password"),
    ClientCertificateRequired = true,
    CheckCertificateRevocation = true,
    // Optional custom validation callback
    RemoteCertificateValidationCallback = (s, cert, chain, errors) => true
};

var service = new NispService();
var client = service.CreateClient("localhost", 1234, sslOptions); // use sslOptions
```
## Protocol Details
### Message Format
1. **Header**: 4-byte little-endian integer indicating payload size
2. **Payload**: Serialized object (compressed if enabled)
### Serialization
Uses [MemoryPack](https://github.com/Cysharp/MemoryPack) for extremely fast binary serialization. Your message types must be MemoryPack-compatible
```csharp
[MemoryPackable]
public partial class MyMessage
{
    public int Id { get; set; }
    public string Data { get; set; }
}
```
## Performance Considerations
- **NoDelay**: TCP_NODELAY is enabled by default to minimize latency    
- **KeepAlive**: TCP keepalive is enabled to detect disconnected peers
- **ReuseAddress**: Socket address reuse is enabled for faster reconnection
- **Async API**: All operations are fully asynchronous for maximum scalability
## Error Handling
All components provide detailed error logging and proper resource cleanup:
- Automatic retry logic for connections    
- Proper disposal of network resources
- Detailed error messages via logging
## Requirements
- .NET 9.0
- MemoryPack
- K4os.Compression.LZ4 (for compression)
- ZLogger (for logging)