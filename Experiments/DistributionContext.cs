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
            modelBuilder.Entity<Item>()
                .HasOne(i => i.Container)
                .WithMany(c => c.ItemChances)
                .HasForeignKey(i => i.ContainerId);
            modelBuilder.Entity<Item>()
                .HasOne(i => i.Distribution)
                .WithMany(d => d.ItemChances)
                .HasForeignKey(i => i.DistributionId);
            modelBuilder.Entity<Container>()
                .HasMany(c => c.ItemChances)
                .WithOne(i => i.Container)
                .HasForeignKey(i => i.ContainerId);
            modelBuilder.Entity<Container>()
                .HasMany(c => c.ProcListEntries)
                .WithOne(p => p.Container)
                .HasForeignKey(p => p.ContainerId);
            modelBuilder.Entity<Distribution>()
              .HasMany(c => c.ProcListEntries)
              .WithOne(p => p.Distribution)
              .HasForeignKey(p => p.DistributionId);
        }


    }
}
