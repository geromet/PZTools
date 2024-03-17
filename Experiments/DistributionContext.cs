using Data.Models.Items.Distributions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class DistributionContext : DbContext
    {
        public DbSet<Distribution> Distributions { get; set; }
        public DbSet<Container> Containers { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ProcListEntry> ProcListEntries { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlite($"Data Source=Distributions.db");

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Distribution>()
                .HasMany(d => d.Containers)
                .WithOne(c => c.Distribution)
                .HasForeignKey(c => c.DistributionId);

            // Define Item relationship with Container
            modelBuilder.Entity<Item>()
                .HasOne(i => i.Container)        // Item has one Container
                .WithMany(c => c.ItemChances)          // Container has many Items
                .HasForeignKey(i => i.ContainerId) // Foreign key
                .OnDelete(DeleteBehavior.Restrict); // Optional: specify delete behavior
            // Define Item relationship with Distribution
            modelBuilder.Entity<Item>()
                .HasOne(i => i.Distribution)     // Item has one Distribution
                .WithMany(d => d.ItemChances)    // Distribution has many Items
                .HasForeignKey(i => i.DistributionId) // Foreign key
                .OnDelete(DeleteBehavior.Restrict); // Optional: specify delete behavior // Optional: specify delete behavior
            modelBuilder.Entity<Container>()
                .HasMany(c => c.ItemChances)
                .WithOne(i => i.Container)
                .HasForeignKey(i => i.ContainerId);

            modelBuilder.Entity<Container>()
                .HasMany(c => c.ProcListEntries)
                .WithOne(p => p.Container)
                .HasForeignKey(p => p.ContainerId);
        }


    }
}
