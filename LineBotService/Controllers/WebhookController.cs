using Line.Messaging;
using Line.Messaging.Webhooks;
using LineBotService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SharedModels.Messages;
using SharedModels.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;


namespace LineBotService.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly string _channelSecret = "9fda4fa32cb3f0b190031741d9a7a947"; // 從配置中獲取
        private readonly IMessageQueue _messageQueue;
        private readonly ILogger<WebhookController> _logger;
        private readonly ILineBotClient _lineBotClient;
        private readonly ILineMessagingClient _lineMessagingClient;
        private readonly IMemoryCache _memoryCache;

        public WebhookController(ILineMessagingClient lineMessagingClient, IMessageQueue messageQueue, ILineBotClient lineBotClient, IMemoryCache memoryCache, ILogger<WebhookController> logger)
        {
            _lineMessagingClient = lineMessagingClient;
            _messageQueue = messageQueue;
            _lineBotClient = lineBotClient;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook()
        {
            // 驗證簽章
            var signature = Request.Headers["X-Line-Signature"];
            var body = await new StreamReader(Request.Body).ReadToEndAsync();
            if (!ValidateSignature(body, signature))
            {
                _logger.LogWarning("Invalid LINE signature");
                return Unauthorized();
            }

            // 解析 Webhook 事件
            var webhookEvent = System.Text.Json.JsonSerializer.Deserialize<SharedModels.Messages.WebhookEvent>(body);

            // 處理每個事件
            foreach (var evt in webhookEvent?.Events ?? [])
            {
                if (evt.Type=="message" && evt.Message.Type=="text")
                {
                    var userMessage = evt.Message.Text;
                    var replyToken = evt.ReplyToken;
                    var userId = evt.Source.UserId;

                    var bug_query_match = Regex.Match(userMessage, @"^query\s+(\w+)$", RegexOptions.IgnoreCase);

                    // 當用戶輸入 "show menu" 時，回覆 ButtonsTemplate
                    if (userMessage.ToLower() == "show menu")
                    {
                        var templateMessage = new TemplateMessage(
                            "Please select an option",
                            new ButtonsTemplate(
                                thumbnailImageUrl: "https://example.com/image.jpg",
                                title: "Menu",
                                text: "Choose an option below",
                                actions: new ITemplateAction[]
                                {
                                    new PostbackTemplateAction("Option 1", "action=option1", "選項 1"),
                                    new PostbackTemplateAction("Option 2", "action=option2", "選項 2"),
                                    new UriTemplateAction("Visit Website", "https://example.com")
                                }
                            )
                        );

                        await _lineMessagingClient.ReplyMessageAsync(replyToken, new[] { templateMessage });
                    }
                    else if (bug_query_match.Success)
                    {
                        var bugId = bug_query_match.Groups[1].Value;

                        // 儲存 Bug ID 到快取（有效期 10 分鐘）
                        _memoryCache.Set($"UserQueryId_{userId}", bugId, TimeSpan.FromMinutes(10));

                        // 回覆提示
                        await _lineMessagingClient.ReplyMessageAsync(replyToken, $"ID {bugId} 已儲存，請點擊圖文選單查詢。");

                    }
                    else
                    {
                        // 將請求發送到 RabbitMQ
                        var request = new LineBotRequest
                        {
                            UserId = userId,
                            ReplyToken = replyToken,
                            Message = userMessage
                        };
                        await _messageQueue.PublishToDirectExchangeAsync("line-bot-exchange", "bug-service-queue", request);
                    }
                    
                }
                else if (evt.Type=="postback")
                {
                    // 處理 Postback 事件（用戶點選按鈕）
                    var replyToken = evt.ReplyToken;
                    var data = evt.Postback.Data;
                    var userId = evt.Source.UserId;

                    if (data == "action=query_bug")
                    {
                        // 從快取取得 Bug ID
                        if (_memoryCache.TryGetValue($"UserQueryId_{userId}", out string? bugId))
                        {
                            // 轉換成查詢要求 發送到 RabbitMQ
                            var postbackRequest = new LineBotRequest
                            {
                                UserId = userId,
                                ReplyToken = replyToken,
                                Message = $"查詢 Bug ID {bugId}"
                            };
                            await _messageQueue.PublishToDirectExchangeAsync("line-bot-exchange", "bug-service-queue", postbackRequest);

                            // 移除快取（可選）
                            _memoryCache.Remove($"UserQueryId_{userId}");
                        }
                        else
                        {
                            await _lineMessagingClient.ReplyMessageAsync(replyToken, "未找到 Bug ID，請先輸入 'query {ID}'。");
                        }
                    }
                    else
                    {
                        string replyText = data switch
                        {
                            "action=option1" => "You selected Option 1!",
                            "action=option2" => "You selected Option 2!",
                            _ => "Unknown option."
                        };

                        await _lineMessagingClient.ReplyMessageAsync(replyToken, replyText);
                    }
                    

                    //// 將 Postback 事件也發送到 RabbitMQ（可選）
                    //var postbackRequest = new LineBotRequest
                    //{
                    //    UserId = evt.Source.UserId,
                    //    ReplyToken = replyToken,
                    //    Message = $"Postback: {data}"
                    //};
                    //await _messageQueue.PublishToDirectExchangeAsync("line-bot-exchange", "bug-service-queue", postbackRequest);
                }
            }

            return Ok();
        }

        private bool ValidateSignature(string body, string signature)
        {
            var secret = Encoding.UTF8.GetBytes(_channelSecret);
            using var hmac = new HMACSHA256(secret);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computedSignature = Convert.ToBase64String(hash);
            return signature == computedSignature;
        }
    }
}
