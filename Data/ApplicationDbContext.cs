using Microsoft.EntityFrameworkCore;
using WasteCollection.Api.Models;

namespace WasteCollection.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
            : base(options)
        {
        }

        public DbSet<Request> Requests { get; set; } = null!;
    }
}
