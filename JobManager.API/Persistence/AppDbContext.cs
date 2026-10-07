using JobManager.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobManager.API.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobAplication> JobApplications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Job>(e =>
            { 
                e.HasKey(j => j.Id);
                e.HasMany(j => j.Applications)
                    .WithOne(a => a.Job)
                    .HasForeignKey(a => a.JobId)
                    .OnDelete(DeleteBehavior.Restrict);
                
            });

            modelBuilder.Entity<JobAplication>()
                .HasKey(ja => ja.Id);

        }
    }
}

