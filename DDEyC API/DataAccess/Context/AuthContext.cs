using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Context
{
    public class AuthContext : DbContext
    {
        private static AuthContext? _instance;

        public AuthContext()
        {
        }

        public AuthContext(DbContextOptions<AuthContext> options) : base(options)
        {
        }

        public DbSet<Users> Users { get; set; }
        public DbSet<Sessions> Sessions { get; set; }
        public DbSet<PasswordRecoveryRequest> PasswordRecoveryRequests { get; set; }
    }
}
