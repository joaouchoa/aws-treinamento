using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace JobManager.API.Workes
{
    public class JobApplicationNotificationWorker : BackgroundService
    {
        private readonly IConfiguration _configuration;

        public JobApplicationNotificationWorker(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var client = new AmazonSQSClient(RegionEndpoint.SAEast1);

            var queueUrl = _configuration["AWS:SQSQueueUrl"] ?? string.Empty;

            while (!stoppingToken.IsCancellationRequested)
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                };
                var response = await client.ReceiveMessageAsync(request, stoppingToken);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    foreach (var message in response.Messages)
                    {
                        Console.WriteLine($"Received message: {message.Body}");

                        var deleteRequest = new DeleteMessageRequest
                        {
                            QueueUrl = queueUrl,
                            ReceiptHandle = message.ReceiptHandle
                        };
                        await client.DeleteMessageAsync(deleteRequest, stoppingToken);
                    }
                }
            }   
        }
    }
}
