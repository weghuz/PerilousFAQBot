using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;

namespace FAQBot.Models
{
    public class FAQDB : DbContext
    {
        private readonly IConfiguration _config;

        public DbSet<FAQEntry> FAQs { get; set; }
        public DbSet<IGTag> FAQsTags { get; set; }
        public DbSet<InfoGraphic> InfoGraphics { get; set; }
        public DbSet<IGTag> InfographicTags { get; set; }
        public DbSet<LackeyApplication> LackeyApplications { get; set; }
        public DbSet<LackeyApproval> LackeyApprovals { get; set; }
        public DbSet<Counter> Counters { get; set; }

        public FAQDB(IConfiguration config)
        {
            _config = config;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // connect to sql server with connection string from app settings
            string connectionString = _config.GetConnectionString("FAQDB");
            options.UseNpgsql(connectionString);
        }
    }
}
