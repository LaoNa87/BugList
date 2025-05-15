using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Polly;

namespace SharedModels.Services;


// 定義通用的訊息佇列介面，包含發送和消費訊息的功能
public interface IMessageQueue : IDisposable
{
    // 直接發送訊息到指定佇列（點對點）
    Task PublishToQueueAsync<T>(string queueName, T message);

    // 發送訊息到 Direct 交換機，根據路由鍵精確分發
    Task PublishToDirectExchangeAsync<T>(string exchange, string routingKey, T message);

    // 發送訊息到 Fanout 交換機，廣播給所有綁定佇列
    Task PublishToFanoutExchangeAsync<T>(string exchange, T message);

    // 訂閱訊息，支持 Fanout 和 Direct 交換機
    Task SubscribeAsync<T>(string exchange, string queue, string exchangeType, Func<T, Task> handler);
}

// 實現 IMessageQueue 介面，提供 RabbitMQ 的訊息發送與消費功能
public class RabbitMQClient : IMessageQueue
{
    private readonly string _hostName; // RabbitMQ 主機名稱
    private readonly IConnectionFactory _connectionFactory; // 連線工廠
    private IConnection _connection; // RabbitMQ 連線
    private IChannel _channel; // RabbitMQ 頻道
    private bool _disposed; // 標記是否已釋放資源

    // 建構子，初始化主機名稱和連線工廠
    public RabbitMQClient(string hostName = "localhost")
    {
        _hostName = hostName;
        _connectionFactory = new ConnectionFactory 
        { 
            HostName = hostName,
            AutomaticRecoveryEnabled = true, // 啟用自動恢復
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10) // 設定恢復間隔
        };
    }

    // 確保連線和頻道可用，使用 Polly 實現重試機制
    private async Task EnsureConnectionAsync()
    {
        // 定義重試策略，最多重試 3 次，每次間隔為 2^retryAttempt 秒
        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"Failed to connect to RabbitMQ (attempt {retryCount}): {exception.Message}. Retrying in {timeSpan.TotalSeconds} seconds...");
                });

        // 執行連線和頻道建立邏輯
        await policy.ExecuteAsync(async () =>
        {
            // 如果連線不存在或已關閉，則重新建立連線
            if (_connection == null || !_connection.IsOpen)
            {
                _connection?.Dispose();
                _connection = await Task.Run(() => _connectionFactory.CreateConnectionAsync());
            }

            // 如果頻道不存在或已關閉，則重新建立頻道
            if (_channel == null || !_channel.IsOpen)
            {
                _channel?.Dispose();
                _channel = await Task.Run(() => _connection.CreateChannelAsync());
            }
        });
    }

    // 直接發送訊息到指定佇列（點對點）
    public async Task PublishToQueueAsync<T>(string queueName, T message)
    {
        await EnsureConnectionAsync(); // 確保連線可用

        // 設定死信佇列參數，處理失敗訊息
        var arguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", "dlx.exchange" },
            { "x-dead-letter-routing-key", "dlx.queue" }
        };

        // 聲明佇列，設置為持久化
        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

        // 將訊息序列化為位元組
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties { Persistent = true }; // 設置訊息持久化

        // 發送訊息到指定佇列，使用預設交換機（exchange 為空）
        await _channel.BasicPublishAsync(exchange: "", routingKey: queueName, mandatory: true, basicProperties: properties, body: body);
    }

    // 發送訊息到 Direct 交換機，根據路由鍵分發
    public async Task PublishToDirectExchangeAsync<T>(string exchange, string routingKey, T message)
    {
        await EnsureConnectionAsync(); // 確保連線可用

        // 聲明 Direct 交換機，設置為持久化
        await _channel.ExchangeDeclareAsync(exchange:exchange, type: ExchangeType.Direct, durable: true, autoDelete: false);

        // 將訊息序列化為位元組
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties { Persistent = true }; // 設置訊息持久化

        // 發送訊息到 Direct 交換機，根據路由鍵分發
        await _channel.BasicPublishAsync(exchange: exchange, routingKey: routingKey, mandatory: !string.IsNullOrEmpty(routingKey), basicProperties: properties, body: body);
    }

    // 發送訊息到 Fanout 交換機，廣播給所有綁定佇列
    public async Task PublishToFanoutExchangeAsync<T>(string exchange, T message)
    {
        await EnsureConnectionAsync(); // 確保連線可用

        // 聲明 Fanout 交換機，設置為持久化
        await _channel.ExchangeDeclareAsync(exchange: exchange, type: ExchangeType.Fanout, durable: true, autoDelete: false);

        // 將訊息序列化為位元組
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties { Persistent = true }; // 設置訊息持久化

        // 發送訊息到 Fanout 交換機，routingKey 設為空（Fanout 忽略路由鍵）
        await _channel.BasicPublishAsync(exchange: exchange, routingKey: "", mandatory: false, basicProperties: properties, body: body);
    }

    // 訂閱訊息，支援 Fanout 和 Direct 交換機
    public async Task SubscribeAsync<T>(string exchange, string queue, string exchangeType, Func<T, Task> handler)
    {
        await EnsureConnectionAsync(); // 確保連線可用

        // 聲明交換機，根據 exchangeType 指定類型（fanout 或 direct）
        await _channel.ExchangeDeclareAsync(exchange: exchange, type: exchangeType, durable: true, autoDelete: false);

        // 設定死信佇列參數，處理失敗訊息
        var arguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", "dlx.exchange" },
            { "x-dead-letter-routing-key", "dlx.queue" }
        };

        // 聲明死信交換機和佇列
        await _channel.ExchangeDeclareAsync("dlx.exchange", ExchangeType.Direct, durable: true);
        await _channel.QueueDeclareAsync("dlx.queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await _channel.QueueBindAsync("dlx.queue", "dlx.exchange", "dlx.queue");

        // 聲明目標佇列，設置為持久化
        await _channel.QueueDeclareAsync(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

        // 根據交換機類型設定路由鍵：Fanout 為空，Direct 使用佇列名稱
        string routingKey = exchangeType == ExchangeType.Fanout ? "" : queue;
        await _channel.QueueBindAsync(queue: queue, exchange: exchange, routingKey: routingKey);

        // 建立非同步消費者
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                // 反序列化訊息
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(body));
                await handler(message); // 處理訊息
                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false); // 確認訊息處理成功
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process message: {ex.Message}");
                // 處理失敗，發送到死信佇列
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        // 開始消費訊息，設置為手動確認（autoAck: false）
        await _channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
    }

    // 釋放資源，實現 IDisposable 介面
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel?.Dispose();
        _connection?.Dispose();
    }
}