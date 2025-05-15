using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedModels.Messages;
using SharedModels.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using UserManagementMicroservice.Data;
using UserManagementMicroservice.Models;

namespace UserManagementMicroservice.Controllers
{
    [Route("user/[controller]/[action]")]  
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private readonly UserDbContext _context;

        public UsersController(UserDbContext context, IMessageQueue messageQueue)
        {
            _context = context;
            _messageQueue = messageQueue;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

        [HttpPost]
        //[Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            user.CreatedAt = DateTime.UtcNow;
            user.PasswordHash = HashPassword(user.PasswordHash); // 假設有一個密碼加密方法
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 發送用戶新增事件
            var userUpdatedMessage = new UserUpdatedMessage
            {
                UserId = user.Id,
                UserData = JsonSerializer.Serialize(user),
                Timestamp = DateTime.UtcNow
            };
            await _messageQueue.PublishToFanoutExchangeAsync( "user-updated-exchange", userUpdatedMessage);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User updatedUser)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // 更新用戶資訊
            user.Username = updatedUser.Username;
            user.PasswordHash = updatedUser.PasswordHash;
            await _context.SaveChangesAsync();

            // 發送用戶修改事件
            var userUpdatedMessage = new UserUpdatedMessage
            {
                UserId = user.Id,
                UserData = JsonSerializer.Serialize(user),
                Timestamp = DateTime.UtcNow
            };
            await _messageQueue.PublishToFanoutExchangeAsync("user-updated-exchange", userUpdatedMessage);

            return Ok(user);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        private string HashPassword(string password)
        {
            // 這裡應該使用真正的密碼加密，例如 BCrypt
            return password; // 僅為範例
        }
    }
}
