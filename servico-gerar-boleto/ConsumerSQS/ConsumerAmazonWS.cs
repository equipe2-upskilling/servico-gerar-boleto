using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Utils;
using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Student.ConsumerAWS.ConsumerSQS
{
    public class ConsumerAmazonWS
    {
        private readonly AmazonSQSClient _sqsClient;
        private readonly string? _queueUrl;

        public ConsumerAmazonWS(string awsAccessKeyId, string awsSecretAccessKey, string awsRegion, string queueName)
        {
            // Configuração do cliente SQS
            var sqsConfig = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
            };

            _sqsClient = new AmazonSQSClient(awsAccessKeyId, awsSecretAccessKey, sqsConfig);

            // Obtém a URL da fila pelo nome
            _queueUrl = GetQueueUrlByName(queueName).GetAwaiter().GetResult();
        }

        public async Task StartListening(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10, // Número máximo de mensagens a serem recebidas de uma vez
                        WaitTimeSeconds = 20 // Tempo de espera (em segundos) para receber novas mensagens
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

                    if (response.Messages.Count > 0)
                    {
                        foreach (var message in response.Messages)
                        {
                            // Processar a mensagem recebida aqui
                            Console.WriteLine($"Mensagem Recebida: {message.Body}");

                            await SendPdfRequest();
                            // Deletar a mensagem da fila após processamento
                            await DeleteMessageFromQueue(message.ReceiptHandle, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Trate a exceção aqui ou apenas lance-a novamente para ser tratada externamente
                    Console.WriteLine($"Erro ao receber mensagens da fila SQS: {ex.Message}");
                }
            }
        }

        private async Task SendPdfRequest()
        {
            // Obtendo o diretório da solução usando Directory.GetCurrentDirectory()
            string solutionDir = Directory.GetCurrentDirectory();

            // Open text file
            // Combinando o diretório da solução com o nome do arquivo desejado
            string filePath = Path.Combine(solutionDir, "templateBoleto.html");

            // Abertura do arquivo usando FileStream
            // Caminho do arquivo PDF
            FileStream htmlSource = File.Open(filePath, FileMode.Open);

            // Create PDF file
            string filePathPdf = Path.Combine(solutionDir, "boleto.pdf");

            FileStream pdfDest = File.Open(filePathPdf, FileMode.OpenOrCreate);

            // Intialize conversion properties
            ConverterProperties converterProperties = new ConverterProperties();
            HtmlConverter.ConvertToPdf(htmlSource, pdfDest, converterProperties);

            Console.WriteLine("PDF do boleto gerado com sucesso!");

            // Send the PDF file to the QueuePutBoleto queue
            // Caminho do arquivo PDF que deseja enviar

            // Verifique se o arquivo PDF existe
            if (!File.Exists(filePathPdf))
            {
                Console.WriteLine("Arquivo PDF do boleto não encontrado.");
                return;
            }
            try
            {
                // Leitura do arquivo PDF para um array de bytes
                byte[] fileBytes = File.ReadAllBytes(filePathPdf);

                // Envio do arquivo para a fila SQS
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MessageBody = Convert.ToBase64String(fileBytes) // Enviar o arquivo codificado em base64
                };

                var response = await _sqsClient.SendMessageAsync(sendMessageRequest);

                Console.WriteLine($"Arquivo de boleto enviado com sucesso para a fila SQS. MessageId: {response.MessageId}");
            }
            catch (AmazonSQSException ex)
            {
                Console.WriteLine($"Erro ao enviar o arquivo de boleto para a fila SQS: {ex.Message}");
            }

        }


        private async Task SendMessageToQueue(string messageBody)
        {
            try
            {
                var request = new SendMessageRequest
                {
                    QueueUrl = "QueuePutBoleto", //  URL para enviar Pdf
                    MessageBody = messageBody,
                };

                var response = await _sqsClient.SendMessageAsync(request);
                Console.WriteLine($"Mensagem enviado with MessageId: {response.MessageId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar mensagem da fila SQS: {ex.Message}");
            }
        }


        private async Task DeleteMessageFromQueue(string receiptHandle, CancellationToken cancellationToken)
        {
            try
            {
                var request = new DeleteMessageRequest
                {
                    QueueUrl = _queueUrl,
                    ReceiptHandle = receiptHandle
                };

                await _sqsClient.DeleteMessageAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao deletar mensagem da fila SQS: {ex.Message}");
            }
        }

        private async Task<string> GetQueueUrlByName(string queueName)
        {
            try
            {
                var request = new GetQueueUrlRequest
                {
                    QueueName = queueName
                };

                var response = await _sqsClient.GetQueueUrlAsync(request);
                return response.QueueUrl;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro: {ex.Message}");
            }
        }
    }
}
