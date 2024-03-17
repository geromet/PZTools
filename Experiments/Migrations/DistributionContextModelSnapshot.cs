﻿// <auto-generated />
using System;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(DistributionContext))]
    partial class DistributionContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.3");

            modelBuilder.Entity("Data.Models.Items.Distributions.Container", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("DistributionId")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("DontSpawnAmmo")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("FillRand")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ItemRolls")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("JunkRolls")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("Procedural")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("DistributionId");

                    b.ToTable("Containers");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Distribution", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("DontSpawnAmmo")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("FillRand")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("IsShop")
                        .HasColumnType("INTEGER");

                    b.Property<long?>("ItemRolls")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("JunkRolls")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MaxMap")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int?>("ProcListEntryId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("StashChance")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Distributions");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Item", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<double?>("Chance")
                        .HasColumnType("REAL");

                    b.Property<int?>("ContainerId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("DistributionId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ContainerId");

                    b.HasIndex("DistributionId");

                    b.ToTable("Items");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.ProcListEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ChildDistributionId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ChildDistributionId1")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ContainerId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("DistributionId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ForceForItems")
                        .HasColumnType("TEXT");

                    b.Property<string>("ForceForRooms")
                        .HasColumnType("TEXT");

                    b.Property<string>("ForceForTiles")
                        .HasColumnType("TEXT");

                    b.Property<string>("ForceForZones")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Max")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Min")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int?>("WeightChance")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ChildDistributionId1");

                    b.HasIndex("ContainerId");

                    b.HasIndex("DistributionId");

                    b.ToTable("ProcListEntries");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Container", b =>
                {
                    b.HasOne("Data.Models.Items.Distributions.Distribution", "Distribution")
                        .WithMany("Containers")
                        .HasForeignKey("DistributionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Distribution");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Item", b =>
                {
                    b.HasOne("Data.Models.Items.Distributions.Container", "Container")
                        .WithMany("ItemChances")
                        .HasForeignKey("ContainerId");

                    b.HasOne("Data.Models.Items.Distributions.Distribution", "Distribution")
                        .WithMany("ItemChances")
                        .HasForeignKey("DistributionId");

                    b.Navigation("Container");

                    b.Navigation("Distribution");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.ProcListEntry", b =>
                {
                    b.HasOne("Data.Models.Items.Distributions.Distribution", "ChildDistribution")
                        .WithMany()
                        .HasForeignKey("ChildDistributionId1");

                    b.HasOne("Data.Models.Items.Distributions.Container", "Container")
                        .WithMany("ProcListEntries")
                        .HasForeignKey("ContainerId");

                    b.HasOne("Data.Models.Items.Distributions.Distribution", "Distribution")
                        .WithMany("ProcListEntries")
                        .HasForeignKey("DistributionId");

                    b.Navigation("ChildDistribution");

                    b.Navigation("Container");

                    b.Navigation("Distribution");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Container", b =>
                {
                    b.Navigation("ItemChances");

                    b.Navigation("ProcListEntries");
                });

            modelBuilder.Entity("Data.Models.Items.Distributions.Distribution", b =>
                {
                    b.Navigation("Containers");

                    b.Navigation("ItemChances");

                    b.Navigation("ProcListEntries");
                });
#pragma warning restore 612, 618
        }
    }
}