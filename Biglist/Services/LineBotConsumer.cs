using Biglist.Data;
using RabbitMQ.Client;
using SharedModels.Messages;
using SharedModels.Services;

namespace Biglist.Services
{
    public class LineBotConsumer : BackgroundService
    {
        private readonly IMessageQueue _messageQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LineBotConsumer> _logger;

        public LineBotConsumer(IMessageQueue messageQueue, IServiceScopeFactory scopeFactory, ILogger<LineBotConsumer> logger)
        {
            _messageQueue = messageQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _messageQueue.SubscribeAsync<LineBotRequest>("line-bot-exchange", "bug-service-queue",ExchangeType.Direct, async request =>
            {
                _logger.LogInformation($"Received LINE message: {request.Message}");

                // 創建一個作用域
                using var scope = _scopeFactory.CreateScope();
                var _dbContext = scope.ServiceProvider.GetRequiredService<BugDbContext>();

                // 假設用戶訊息格式為 "查詢 Bug ID 123"
                string responseMessage;
                if (request.Message.StartsWith("查詢 Bug ID"))
                {
                    var bugIdStr = request.Message.Split(" ").Last();
                    if (int.TryParse(bugIdStr, out var bugId))
                    {
                        var bug = await _dbContext.Bugs.FindAsync(bugId);
                        responseMessage = bug != null
                            ? $"Bug ID {bugId}: {bug.Title} - {bug.Status}"
                            : $"找不到 Bug ID {bugId}";
                    }
                    else
                    {
                        responseMessage = "請提供有效的 Bug ID，例如：查詢 Bug ID 123";
                    }
                }
                else
                {
                    responseMessage = "請使用格式：查詢 Bug ID <編號>";
                }

                // 將回覆發送到 RabbitMQ
                var response = new LineBotResponse
                {
                    ReplyToken = request.ReplyToken,
                    ResponseMessage = responseMessage
                };
                await _messageQueue.PublishToDirectExchangeAsync("line-bot-reply-exchange", "line-bot-reply-queue", response);
            });
        }
    }
}
