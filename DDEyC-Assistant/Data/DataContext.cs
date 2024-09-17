using DDEyC_Assistant.Models;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_Assistant.Data
{
    public class DataContext : DbContext
    {
        public DataContext()
        {
        }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {


        }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
        }
    }
}
