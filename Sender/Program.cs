using Broker; // namespace din broker.proto
using Grpc.Net.Client;

// ✅ activăm suportul pentru HTTP/2 fără TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Adresă broker
var brokerAddress = "http://localhost:5000";
using var channel = GrpcChannel.ForAddress(brokerAddress);
var client = new BrokerService.BrokerServiceClient(channel);

Console.WriteLine("Sender pornit. Scrie mesajele pe care vrei să le trimiți.");
Console.WriteLine("Scrie 'exit' pentru a închide aplicația.\n");

while (true)
{
    Console.Write("Subiect: ");
    var subject = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(subject) || subject.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.Write("Conținut: ");
    var content = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(content) || content.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    var message = new Message
    {
        Subject = subject,
        Content = content
    };

    var ack = await client.SendMessageAsync(message);

    Console.WriteLine(ack.Success
        ? "[Sender] Mesaj trimis cu succes!\n"
        : "[Sender] Eroare la trimiterea mesajului.\n");
}