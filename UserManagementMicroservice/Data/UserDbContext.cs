using Microsoft.EntityFrameworkCore;
using UserManagementMicroservice.Models;

namespace UserManagementMicroservice.Data
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}
