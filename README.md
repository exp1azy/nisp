# NISP (.NET Inter-Service Protocol)
**NISP is a high-performance communication protocol designed specifically for .NET inter-service communication.** It provides fast, efficient serialization and transmission of .NET objects between services with minimal overhead.
## Key Features
- Fast serialization using **MemoryPack**
- **LZ4, Snappier or Zstdsharp compression** to choose for reduced network traffic
- **SSL** support for secure communication
- **Logging**
- **Full-duplex communication** through the peer implementation
- **Modern async API** with cancellation support
- **TCP-based** reliable transmission
## Quick Start
### Sender
Create a console application that will be a NISP **sender** that sends fire-and-forget messages
```csharp
static async Task Main(string[] args)
{
    const string host = "127.0.0.1";
    const int port = 7777;

    var service = new NispService()
        .WithMessageTypes((1, typeof(UserMessage))) // declare your message types
        .WithLogging(builder => builder.AddConsole()) // use logging
        .WithCompression(b => b.UseLZ4()); // use compression

    var sender = service.CreateSender(host, port);
    await sender.ConnectAsync();

    for (int i = 0; i < 20; i++)
    {
        string message = $"Hello from sender {i}";

        await sender.SendAsync(new UserMessage
        {
            Id = i,
            Message = message,
            MessageBytes = Encoding.UTF8.GetBytes(message),
            RandomNumber = Random.Shared.Next(),
            Time = DateTime.Now
        });

        await sender.SendAsync(new ImAliveMessage // built-in message type for checking the service's functionality
        {
            Id = Guid.NewGuid(),
            DateTime = DateTime.Now
        });
    }

    await sender.StopAsync();
}
```
### Receiver
Create a console application that will be a NISP **receiver** that accepts messages from the client
```csharp
static async Task Main(string[] args)
{
    const string host = "127.0.0.1";
    const int port = 7777;

    var service = new NispService()
        .WithMessageTypes((1, typeof(UserMessage))) // declare the same types that you use for sender
        .WithCompression(b => b.UseLZ4()) // use the same compression that you use for sender
        .WithLogging(builder => builder.AddConsole()); // use logging

    var receiver = service.CreateReceiver(host, port);
    await receiver.ListenAsync();
    receiver.StartReceiving();

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
```
### Peer
Create a console application that will be a NISP peer that initializes a sender to send a message to a remote node, as well as a receiver to receive messages from a remote node.
#### Nisp.Adam
```csharp
static class Program
{
    static async Task Main(string[] args)
    {
        var service = new NispService()
            .WithMessageTypes((1, typeof(UserMessage))) // declare types
            .WithCompression(f => f.UseSnappier()) // use compression if needed
            .WithLogging(b => b.AddConsole()); // use logging

        var peer = service.CreatePeer(new PeerConfig
        {
            SenderEndpoint = ("127.0.0.1", 5001),
            ReceiverEndpoint = ("127.0.0.1", 5000),
            ImAliveConfig = new ImAliveConfig 
            {
                DelayInMilliseconds = 5000 // specify this if you want this peer to send an ImAliveMessage every N milliseconds to remote peer
            }
        });

        await peer.ConnectAsync();
        await peer.SendAsync(new UserMessage { Message = "Hello Eve" });

        var t1 = Task.Run(async () =>
        {
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Eve: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Hello Eve" });
            }
        });

        var t2 = Task.Run(async () =>
        {
            await foreach (var message in peer.ReceiveAsync<ImAliveMessage>())
            {
                Console.WriteLine($"Eve is alive");
            }
        });

        await Task.WhenAll(t1, t2);
        await peer.CloseConnectionsAsync();
    }
}
```
#### Nisp.Eve
```csharp
static class Program
{
    static async Task Main(string[] args)
    {
        var service = new NispService()
            .WithMessageTypes((1, typeof(UserMessage))) // declare the same types that you use for Adam
            .WithCompression(f => f.UseSnappier()) // use the same compression that you use for Adam
            .WithLogging(b => b.AddConsole()); // use logging

        var peer = service.CreatePeer(new PeerConfig
        {
            SenderEndpoint = ("127.0.0.1", 5000),
            ReceiverEndpoint = ("127.0.0.1", 5001),
            ImAliveConfig = new ImAliveConfig
            {
                DelayInMilliseconds = 5000 // specify this if you want this peer to send an ImAliveMessage every N milliseconds to remote peer
            }
        });

        await peer.ConnectAsync();

        var t1 = Task.Run(async () =>
        {
            await foreach (var message in peer.ReceiveAsync<UserMessage>())
            {
                Console.WriteLine($"\nReceived message from Adam: {message.Message}\n");
                await peer.SendAsync(new UserMessage { Message = "Hello Adam" });
                await Task.Delay(1000);
            }
        });

        var t2 = Task.Run(async () =>
        {
            await foreach (var message in peer.ReceiveAsync<ImAliveMessage>())
            {
                Console.WriteLine($"Adam is alive");
            }
        });

        await Task.WhenAll(t1, t2);
        await peer.CloseConnectionsAsync();
    }
}
```
### Set Up The Service
Customize the service according to your needs. The settings apply to all the components that you use
```csharp
var service = new NispService()
    .WithCompression(f => f.UseSnappier()) // use LZ4, ZstdSharp or Snappier compression
    .WithLogging(b => b.AddConsole()) // use logging
    .WithMessageTypes(
        (1, typeof(UserMessage)), // declare
        (2, typeof(OrderMessage)), // your own
        (3, typeof(PaymentMessage)) // message types
    ); 
    
```
Each message type must have its own unique identifier, and all components must define identifiers and types in agreement
## Components
### 1. `NispSender`
The sender component for sending messages to a NISP receiver
```csharp
var service = new NispService().WithMessageTypes((1, typeof(UserMessage)));

var sender = service.CreateSender(host, port); // create sender
await sender.ConnectAsync(); // connect to receiver
await sender.SendAsync(new MyMessage { ... }); // send a message
```
### 2. `NispReceiver`
The receiver component for receiving messages
```csharp
var service = new NispService().WithMessageTypes((1, typeof(UserMessage)));

var receiver = service.CreateReceiver(host, port); // create receiver
await receiver.ListenAsync(); // listen the sender
receiver.StartReceiving(); // starts receive

// receive messages
await foreach (var message in listener.ReceiveAsync<MyMessage>())
{
    // process message
}
```
### 3. `NispPeer`
A bidirectional communication peer combining both sender and receiver capabilities
```csharp
var service = new NispService().WithMessageTypes((1, typeof(UserMessage)));

var peer = service.CreatePeer(new PeerConfig // create peer
{
    SenderEndpoint = ("127.0.0.1", 5001),
    ReceiverEndpoint = ("127.0.0.1", 5000),
    ImAliveConfig = new ImAliveConfig // if specified, sends an ImAliveMessage to the remote peer every 5000 milliseconds
    {
        DelayInMilliseconds = 5000
    }
});

// receive messages
await foreach (var message in peer.ReceiveAsync<MyMessage>())
{
    // process message
}
```
## Encryption
Configure SSL using `SslOptions`
```csharp
// load certificates
var serverCertificate = new X509Certificate2("server.pfx", "password123");
var rootCACertificate = new X509Certificate2("root.cer");

// receiver
var receiverSslOptions = new SslOptions
{
    Certificate = serverCertificate,
    ClientCertificateRequired = false,
    CheckCertificateRevocation = false,
    RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
};

// sender
var senderSslOptions = new SslOptions
{
    Certificate = null,
    CheckCertificateRevocation = false,
    RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
};

// create service
var service = new NispService()
    .WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
    .WithCompression(factory => factory.UseZstdSharp());

// create receiver
var receiver = service.CreateReceiver("127.0.0.1", 5555, receiverSslOptions);
        
// create sender
var sender = service.CreateSender("127.0.0.1", 5555, senderSslOptions);
```
## Protocol Details
### Message Format
```bash
[Compressed(1 byte) + TypeId(2 bytes) + PayloadLength(4 bytes) + Payload]
```
1. **Header**
- The first byte stores information about whether the message is compressed
- The next two bytes store the message type identifier
- The remaining four bytes are reserved for the payload size
2. **Payload**
### Serialization
Uses [MemoryPack](https://github.com/Cysharp/MemoryPack) for extremely fast binary serialization. Your message types must be MemoryPack-compatible
```csharp
[MemoryPackable]
public partial class MyMessage
{
    public int Id { get; set; }
    public string Text { get; set; }
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
- .NET 9.0 or .NET 10.0