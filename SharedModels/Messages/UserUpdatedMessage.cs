
namespace SharedModels.Messages
{
    public class UserUpdatedMessage
    {
        public int UserId { get; set; }
        public string UserData { get; set; } // JSON 格式的用戶資訊
        public DateTime Timestamp { get; set; }
    }
}
