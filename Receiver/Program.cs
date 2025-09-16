using Broker;
using Grpc.Core;
using Grpc.Net.Client;

// Activăm suportul pentru HTTP/2 fără TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var brokerAddress = "http://localhost:5000";
using var channel = GrpcChannel.ForAddress(brokerAddress);
var client = new BrokerService.BrokerServiceClient(channel);

Console.WriteLine("Receiver pornit.");
Console.Write("Introdu subiectul pe care vrei să-l urmărești: ");
var subject = Console.ReadLine();

var request = new SubscribeRequest
{
    ReceiverId = "receiver1", // poți face unic pt fiecare receiver
    Subject = subject
};

using var call = client.Subscribe(request);

Console.WriteLine($"Receiver abonat la subiectul: {subject}\n");

await foreach (var message in call.ResponseStream.ReadAllAsync())
{
    Console.WriteLine($"[Receiver] {message.Subject}: {message.Content}");
}