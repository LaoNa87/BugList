using Biglist.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SharedModels.Entities;
using SharedModels.Messages;
using SharedModels.Services;

namespace Biglist.Services
{
    public class UserSyncConsumer : BackgroundService
    {
        private readonly IMessageQueue _messageQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UserSyncConsumer> _logger;

        public UserSyncConsumer(IMessageQueue messageQueue, IServiceScopeFactory scopeFactory, ILogger<UserSyncConsumer> logger)
        {
            _messageQueue = messageQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting UserSyncConsumer...");

            await _messageQueue.SubscribeAsync<UserUpdatedMessage>("user-updated-exchange", "bug-user-updated-consumer", ExchangeType.Fanout, async message =>
            {
                // 創建一個作用域
                using var scope = _scopeFactory.CreateScope();
                var _context = scope.ServiceProvider.GetRequiredService<BugDbContext>();

                _logger.LogInformation($"Received user update: UserId={message.UserId},  Timestamp={message.Timestamp}");

                var user = await _context.User.FindAsync(message.UserId);
                if (user == null)
                {
                    user = new UserEasy { Id = message.UserId };
                    _context.User.Add(user);
                }
                else if (user.LastUpdated > message.Timestamp)
                {
                    _logger.LogWarning($"Ignoring outdated message for UserId={message.UserId}, MessageTimestamp={message.Timestamp}, LocalTimestamp={user.LastUpdated}");
                    return;
                }

                user.UserData = message.UserData;
                user.LastUpdated = message.Timestamp;

                await _context.Database.OpenConnectionAsync();
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.[User] ON;"); // 允許插入identity欄位
                    await _context.SaveChangesAsync();
                    await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.[User] OFF;");
                }
                finally
                {
                    await _context.Database.CloseConnectionAsync();
                }

                


                _logger.LogInformation($"Updated user in database: UserId={message.UserId}, UserData={message.UserData}");
            });

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
