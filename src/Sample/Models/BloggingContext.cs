using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Test.Models
{
    public partial class BloggingContext : DbContext
    {
        public virtual DbSet<Blog> Blogs { get; set; }
        public virtual DbSet<BlogPost> BlogPosts { get; set; }
        public virtual DbSet<BlogSetting> BlogSettings { get; set; }
        public virtual DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(@"Server=(local)\sqlexpress;Database=Blogging;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>(entity =>
            {
                entity.ToTable("Blog");

                entity.HasIndex(e => e.UserId)
                    .HasName("IX_FK_Blog_User");

                entity.Property(e => e.DateCreate).HasColumnType("datetime");

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Blogs)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Blog_User");
            });

            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.ToTable("BlogPost");

                entity.HasIndex(e => e.BlogId)
                    .HasName("IX_FK_BlogPost_Blog");

                entity.Property(e => e.Body)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(e => e.DatePublication).HasColumnType("datetime");

                entity.HasOne(d => d.Blog)
                    .WithMany(p => p.BlogPosts)
                    .HasForeignKey(d => d.BlogId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_BlogPost_Blog");
            });

            modelBuilder.Entity<BlogSetting>(entity =>
            {
				entity.ToTable("BlogSettings");

				entity.HasKey(e => e.BlogId);

                entity.Property(e => e.BlogId).ValueGeneratedNever();

                entity.HasOne(d => d.Blog)
                    .WithOne(p => p.BlogSetting)
                    .HasForeignKey<BlogSetting>(d => d.BlogId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_BlogSettings_Blog");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");

                entity.Property(e => e.DateCreate).HasColumnType("datetime");

                entity.Property(e => e.Name1)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false).HasColumnName("Name");
            });
		}
    }
}
