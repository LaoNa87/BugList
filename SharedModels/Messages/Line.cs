
using System.Text.Json.Serialization;

namespace SharedModels.Messages
{
    public class WebhookEvent
    {
        [JsonPropertyName("events")]
        public Event[] Events { get; set; }
    }

    public class Event
    {

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("message")]
        public Message Message { get; set; }

        [JsonPropertyName("postback")]
        public Postback Postback { get; set; }

        [JsonPropertyName("replyToken")]
        public string ReplyToken { get; set; }

        [JsonPropertyName("source")]
        public Source Source { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class Postback
    {
        [JsonPropertyName("data")]
        public string Data { get; set; }
    }

    public class Source
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class LineBotRequest
    {
        public string UserId { get; set; }
        public string ReplyToken { get; set; }
        public string Message { get; set; }
    }

    public class LineBotResponse
    {
        public string ReplyToken { get; set; }
        public string ResponseMessage { get; set; }
    }
}
