
namespace SharedModels.Entities
{
    public class UserEasy 
    {
        public int Id { get; set; }
        public string UserData { get; set; } // JSON 格式的用戶資訊
        public DateTime LastUpdated { get; set; }
    }
}
