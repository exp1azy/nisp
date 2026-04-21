# NISP (.NET Inter-Service Protocol)
**NISP is a high-performance communication protocol designed specifically for .NET inter-service communication.** It provides fast, efficient serialization and transmission of .NET objects between services with minimal overhead.
## Key Features
| Feature | Description |
|---------|-------------|
| 🚀 **High Performance** | Binary serialization with MemoryPack for maximum speed |
| 🔄 **Full-Duplex** | Bidirectional communication via `NispPeer` |
| 🗜️ **Compression** | LZ4 (fastest), Snappier, or Zstd (better ratio) |
| 🔒 **Security** | SSL/TLS encryption support with certificate validation |
| 📝 **Logging** | Built-in Microsoft.Extensions.Logging integration |
| ❤️ **Heartbeat** | Automatic `ImAliveMessage` for health monitoring |
| ✅ **Message Acknowledgment** | Optional delivery confirmation |
| 🔁 **Auto-Reconnection** | Configurable retry logic with exponential backoff |
| 🎯 **Type Safety** | Strongly-typed messages with runtime type routing |
## Quick Start
### Define Your Messages
```csharp
using MemoryPack;

[MemoryPackable]
public partial class UserMessage
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```
### Configure the Service
```csharp
var service = new NispService()
    .WithMessageTypes((1, typeof(UserMessage)))  // Register message types
    .WithCompression(f => f.UseLZ4())            // Optional: LZ4, ZstdSharp or Snappier compression
    .WithLogging(b => b.AddConsole());           // Optional: logging
```
### Create a Sender
Create a console application that will be a NISP **sender** that sends fire-and-forget messages
```csharp
var sender = service.CreateSender("127.0.0.1", 7777);
await sender.ConnectAsync();

await sender.SendAsync(new UserMessage { Message = "Hello, World!" });
await sender.StopAsync();
```
### Create a Receiver
Create a console application that will be a NISP **receiver** that accepts messages from the sender
```csharp
var receiver = service.CreateReceiver("127.0.0.1", 7777);
await receiver.ListenAsync();
receiver.StartReceiving(ReceiveErrorBehavior.Ignore);

await foreach (var msg in receiver.ReceiveAsync<UserMessage>())
{
    Console.WriteLine($"Received: {msg.Message}");
}
```
### Create a Bidirectional Peer
Create a console application that will be a NISP peer that initializes a sender to send a message to a remote node, as well as a receiver to receive messages from a remote node.
```csharp
var peer = service.CreatePeer(new PeerConfig
{
    SenderEndpoint = ("127.0.0.1", 5001),
    ReceiverEndpoint = ("127.0.0.1", 5000),
    ImAliveConfig = new ImAliveConfig { DelayInMilliseconds = 5000 },
    Acknowledge = true
});

await peer.ConnectAsync();
peer.StartReceiving(ReceiveErrorBehavior.Ignore);

// Send and receive simultaneously
await peer.SendAsync(new UserMessage { Message = "Hello" });

await foreach (var msg in peer.ReceiveAsync<UserMessage>())
{
    Console.WriteLine($"Received: {msg.Message}");
    await peer.SendAsync(new UserMessage { Message = "Reply" });
}
```
## Components
### `NispSender` - One-Way Communication
Sends messages to a remote receiver without expecting a response
```csharp
var sender = service.CreateSender("127.0.0.1", 8080);
await sender.ConnectAsync(retryAttempts: 3, retryDelay: 5000);
await sender.SendAsync(new MyMessage { Data = "Hello" });
```
### `NispReceiver` - Message Listener
Listens for incoming messages and routes them to type-specific channels
```csharp
var receiver = service.CreateReceiver("0.0.0.0", 8080);
await receiver.ListenAsync();
receiver.StartReceiving(ReceiveErrorBehavior.Ignore);

// Subscribe to specific message types
await foreach (var msg in receiver.ReceiveAsync<UserMessage>())
{
    ProcessUserMessage(msg);
}

await foreach (var heartbeat in receiver.ReceiveAsync<ImAliveMessage>())
{
    UpdateHealthStatus(heartbeat);
}
```
### `NispPeer` - Bidirectional Communication
Combines sender and receiver for full-duplex communication. Ideal for microservices that need to both send and receive.
```csharp
var peer = service.CreatePeer(new PeerConfig
{
    SenderEndpoint = ("service-b.internal", 5001),
    ReceiverEndpoint = ("0.0.0.0", 5000),
    ImAliveConfig = new ImAliveConfig { DelayInMilliseconds = 10000 },
    Acknowledge = true
});

await peer.ConnectAsync();
peer.StartReceiving();
```
## Configuration
### Message Type Registration
Each message type must have a unique ushort identifier (1-65535). Both communicating parties must use identical ID-to-Type mappings
```csharp
var service = new NispService()
    .WithMessageTypes(
        (1, typeof(UserMessage)),
        (2, typeof(OrderMessage)),
        (3, typeof(PaymentMessage)),
        (4, typeof(OtherMessage))
    );
```
### Compression Options
```csharp
// LZ4 - fastest (recommended for most scenarios)
.WithCompression(f => f.UseLZ4())

// Zstd - best compression ratio
.WithCompression(f => f.UseZstdSharp())

// Snappier - Google's algorithm
.WithCompression(f => f.UseSnappier())
```
### Error Handling Behavior
```csharp
receiver.StartReceiving(ReceiveErrorBehavior.StopAndThrow); // Default - stop and throw an exception
receiver.StartReceiving(ReceiveErrorBehavior.Stop);         // Stop gracefully
receiver.StartReceiving(ReceiveErrorBehavior.Ignore);       // Skip and continue
```
### SSL/TLS Encryption
```csharp
// Load certificates
var serverCert = new X509Certificate2("server.pfx", "password");
var clientCert = new X509Certificate2("client.pfx", "password");

// Server-side (receiver)
var receiverSsl = new SslOptions
{
    Certificate = serverCert,
    ClientCertificateRequired = true,
    CheckCertificateRevocation = true
};

// Client-side (sender)
var senderSsl = new SslOptions
{
    Certificate = clientCert,
    RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
};

var receiver = service.CreateReceiver("0.0.0.0", 443, receiverSsl);
var sender = service.CreateSender("0.0.0.0", 443, senderSsl);
```
## Protocol Details
### Packet Structure
```bash
┌─────────────┬──────────────┬───────────────┬─────────────────┐
│  Compressed │   TypeId     │  PayloadLen   │    Payload      │
│   1 byte    │   2 bytes    │   4 bytes     │   N bytes       │
├─────────────┼──────────────┼───────────────┼─────────────────┤
│ 0x00/0x01   │  ushort LE   │   int LE      │   Serialized    │
└─────────────┴──────────────┴───────────────┴─────────────────┘
```
- Compressed: 0x01 if payload is compressed, 0x00 otherwise
- TypeId: Message type identifier (must match registered types)
- PayloadLen: Length of the payload in bytes
- Payload: MemoryPack-serialized (and optionally compressed) message
### Serialization 
NISP uses MemoryPack - the fastest binary serializer for .NET. Your message classes must be marked with [MemoryPackable]:
```csharp
[MemoryPackable]
public partial class MyMessage
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    
    [MemoryPackIgnore]
    public string ComputedField => $"{Id}: {Text}";
}
```
## Advanced Scenarios
### Heartbeat Monitoring
```csharp
var peer = service.CreatePeer(new PeerConfig
{
    SenderEndpoint = ("0.0.0.1", 5001),
    ReceiverEndpoint = ("0.0.0.0", 5000),
    ImAliveConfig = new ImAliveConfig { DelayInMilliseconds = 5000 },
    Acknowledge = false
});

// Monitor remote peer health
await foreach (var alive in peer.ReceiveAsync<ImAliveMessage>())
{
    Console.WriteLine($"Peer {alive.Id} is alive at {alive.DateTime}");
}
```
### Message Acknowledgment
```csharp 
var peer = service.CreatePeer(new PeerConfig
{
    // ... other config
    Acknowledge = true  // Automatically send AckMessage for every received message
});

// The sender can wait for acknowledgment
await peer.SendAsync(new ImportantMessage { Data = "critical" });
// AckMessage is sent automatically by the receiver
```
### Multiple Message Handlers
```csharp
// Handle different message types in parallel
var userTask = Task.Run(async () =>
{
    await foreach (var msg in peer.ReceiveAsync<UserMessage>())
        Console.WriteLine($"User: {msg.Name}");
});

var orderTask = Task.Run(async () =>
{
    await foreach (var msg in peer.ReceiveAsync<OrderMessage>())
        await ProcessOrder(msg);
});

var heartbeatTask = Task.Run(async () =>
{
    await foreach (var msg in peer.ReceiveAsync<ImAliveMessage>())
        Console.WriteLine($"Heartbeat from {msg.Id}");
});

await Task.WhenAll(userTask, orderTask, heartbeatTask);
```
## Performance Tuning
### Socket Options (Auto-Configured)
- `NoDelay` (TCP_NODELAY): Minimizes latency
- `KeepAlive`: Detects dead connections
- `ReuseAddress`: Faster reconnection
## Requirements
.NET 9.0 or .NET 10.0