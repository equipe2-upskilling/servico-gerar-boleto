using iText.Html2pdf;

using Student.ConsumerAWS.ConsumerSQS;
using System.Configuration;

// Configurações da AWS
string? awsAccessKeyId = ConfigurationManager.AppSettings["AWSAccessKeyId"];
string? awsSecretAccessKey = ConfigurationManager.AppSettings["AWSSecretAccessKey"];
string? awsRegion = ConfigurationManager.AppSettings["AWSRegion"];
string? queueName = ConfigurationManager.AppSettings["QueueName"];

if (awsAccessKeyId == null) throw new Exception("Chave de Acesso faltando.");
if (awsSecretAccessKey == null) throw new Exception("Segredo da Chave de Acesso faltando.");
if (awsRegion == null) throw new Exception("Região faltando.");
if (queueName == null) throw new Exception("Nome da fila faltando.");

// Criação do ConsumerAWS
var consumer = new ConsumerAmazonWS(awsAccessKeyId, awsSecretAccessKey, awsRegion, queueName);

// Token de cancelamento para parar o consumo
using var cancellationTokenSource = new CancellationTokenSource();

// Iniciar o consumo de mensagens em um loop infinito com intervalo de 10 segundos
while (!cancellationTokenSource.Token.IsCancellationRequested)
{
    Console.WriteLine("Esperando Mensagem para gerar boleto....");
    await consumer.StartListening(cancellationTokenSource.Token);
    await Task.Delay(TimeSpan.FromSeconds(10), cancellationTokenSource.Token);
}

