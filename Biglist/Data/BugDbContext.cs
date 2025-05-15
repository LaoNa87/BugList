using Biglist.Models;
using Microsoft.EntityFrameworkCore;
using SharedModels.Entities;

namespace Biglist.Data
{
    public class BugDbContext : DbContext
    {
        public BugDbContext(DbContextOptions<BugDbContext> options) : base(options) { }

        public DbSet<Bug> Bugs { get; set; }
        public DbSet<UserEasy> User { get; set; }
    }
}
