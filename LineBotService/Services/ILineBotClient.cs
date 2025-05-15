using System.Net.Http.Headers;

namespace LineBotService.Services
{
    public interface ILineBotClient
    {
        Task ReplyMessageAsync(string replyToken, string message);
    }

    public class LineBotClient : ILineBotClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _channelAccessToken = "Fin1uGJcg5yP9X9JVF8Vt8c7lXYgwaUL5h5+HxN0YBmx1Hr+Utbpj3RATZOZlsFVXIMLDgXgVTTpxKgui+VGAelL17LmqzQ7xsaJt8HvPWfbZtYx32iSICRaYmetqW4ZpJyTcXK01xZ1znybZxG/wQdB04t89/1O/w1cDnyilFU=";

        public LineBotClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
        }

        public async Task ReplyMessageAsync(string replyToken, string message)
        {
            var requestBody = new
            {
                replyToken,
                messages = new[] { new { type = "text", text = message } }
            };
            var response = await _httpClient.PostAsJsonAsync("https://api.line.me/v2/bot/message/reply", requestBody);
            response.EnsureSuccessStatusCode();
        }
    }
}
