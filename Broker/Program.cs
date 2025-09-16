using Broker;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;

// Thread Safe - mai multi S/R pot interactiona cu B
// string - receiverID, ConcurrentQueue - mesajele care trebuie trimise la R
var receiverQueues = new ConcurrentDictionary<string, ConcurrentQueue<Message>>();

// Buffer global pentru mesajele pe subiect
var messageBuffer = new ConcurrentDictionary<string, List<Message>>();

// Instanta aplicatie web - pentru serviciile gRPC
var builder = WebApplication.CreateBuilder(args);

// Configurare Kestrel (server web in ASP.NET) - HTTP/2, port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2);
});

// Adaugarea serviciilor gRPC pentru gestionarea apelurilor
builder.Services.AddGrpc();
builder.Services.AddSingleton(receiverQueues);
builder.Services.AddSingleton(messageBuffer);

// Construieste build cu config de mai sus
var app = builder.Build();

app.MapGrpcService<BrokerServiceImpl>();
app.MapGet("/", () => "Broker gRPC server is running (HTTP/2 on port 5000).");

app.Run();

public class BrokerServiceImpl : BrokerService.BrokerServiceBase
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Message>> _receiverQueues;
    private readonly ConcurrentDictionary<string, List<Message>> _messageBuffer;

    // Constructor
    public BrokerServiceImpl(
        ConcurrentDictionary<string, ConcurrentQueue<Message>> receiverQueues,
        ConcurrentDictionary<string, List<Message>> messageBuffer)
    {
        _receiverQueues = receiverQueues;
        _messageBuffer = messageBuffer;
    }

    //ServerCallContext - info despre apel gRPC
    public override Task<Ack> SendMessage(Message request, ServerCallContext context)
    {
        // Adauga mesaj in coada fiecarui receiver
        foreach (var queue in _receiverQueues.Values)
        {
            queue.Enqueue(request);
        }

        // Adauga mesaj in buffer-ul global (pe subiect)
        var list = _messageBuffer.GetOrAdd(request.Subject, _ => new List<Message>());
        lock (list)
        {
            list.Add(request);
        }

        Console.WriteLine($"[Broker] Received: {request.Subject} - {request.Content}");
        return Task.FromResult(new Ack { Success = true });
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
    {
        // Coada pentru fiecare R (se creeaza daca nu este)
        var queue = _receiverQueues.GetOrAdd(request.ReceiverId, _ => new ConcurrentQueue<Message>());

        Console.WriteLine($"[Broker] Receiver {request.ReceiverId} subscribed to subject '{request.Subject}'.");

        // Cauta daca in buffer sunt mesaje specifice (subiect)
        if (_messageBuffer.TryGetValue(request.Subject, out var historical))
        {
            List<Message> copy;
            lock (historical)
            {
                copy = new List<Message>(historical); // facem copie sincron
            }

            foreach (var msg in copy)
            {
                await responseStream.WriteAsync(msg);
                Console.WriteLine($"[Broker] Sent historical to {request.ReceiverId}: {msg.Subject} - {msg.Content}");
            }
        }
        
        // Loop normal pentru mesajele noi
        while (!context.CancellationToken.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var msg))
            {
                if (msg.Subject == request.Subject)
                {
                    await responseStream.WriteAsync(msg);
                    Console.WriteLine($"[Broker] Sent to {request.ReceiverId}: {msg.Subject} - {msg.Content}");
                }
            }
            else
            {
                await Task.Delay(500);
            }
        }
    }
}
