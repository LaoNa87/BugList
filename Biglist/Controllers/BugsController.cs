using Biglist.Data;
using Biglist.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace Biglist.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class BugsController : ControllerBase
    {
        private readonly BugDbContext _context;

        public BugsController(BugDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetBugs()
        {
            var bugs = await _context.Bugs.ToListAsync();
            return Ok(bugs);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateBug([FromBody] Bug bug)
        {
            // 從 JWT Token 中獲取用戶名稱
            var user_id = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(user_id)) return Unauthorized("User ID not found in token.");

            bug.CreatedAt = DateTime.UtcNow;
            bug.CreatedBy = user_id;
            _context.Bugs.Add(bug);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBug), new { id = bug.Id }, bug);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBug(int id)
        {
            var bug = await _context.Bugs.FindAsync(id);
            if (bug == null) return NotFound();
            return Ok(bug);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateBug(int id, [FromBody] Bug bug)
        {
            if (id != bug.Id) return BadRequest();
            _context.Entry(bug).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteBug(int id)
        {
            var bug = await _context.Bugs.FindAsync(id);
            if (bug == null) return NotFound();
            _context.Bugs.Remove(bug);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
