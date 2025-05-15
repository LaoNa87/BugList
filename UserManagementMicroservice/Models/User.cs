namespace UserManagementMicroservice.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; } // 儲存加密後的密碼
        public string Role { get; set; } // 例如 "Admin" 或 "User"
        public DateTime CreatedAt { get; set; }
    }
}
