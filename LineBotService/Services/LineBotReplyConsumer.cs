using RabbitMQ.Client;
using SharedModels.Messages;
using SharedModels.Services;

namespace LineBotService.Services
{
    public class LineBotReplyConsumer : BackgroundService
    {
        private readonly IMessageQueue _messageQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LineBotReplyConsumer> _logger;

        public LineBotReplyConsumer(IMessageQueue messageQueue, IServiceScopeFactory scopeFactory, ILogger<LineBotReplyConsumer> logger)
        {
            _messageQueue = messageQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 創建一個作用域
            using var scope = _scopeFactory.CreateScope();
            var _lineBotClient = scope.ServiceProvider.GetRequiredService<ILineBotClient>();
            
            await _messageQueue.SubscribeAsync<LineBotResponse>("line-bot-reply-exchange", "line-bot-reply-queue",ExchangeType.Direct, async response =>
            {
                _logger.LogInformation($"Sending reply to LINE: {response.ResponseMessage}");
                await _lineBotClient.ReplyMessageAsync(response.ReplyToken, response.ResponseMessage);
            });
        }
    }
}
