using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence;

public sealed class KhazarDbContext(DbContextOptions<KhazarDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<NewsletterSubscription> NewsletterSubscriptions => Set<NewsletterSubscription>();
    public DbSet<ContentSection> ContentSections => Set<ContentSection>();
    public DbSet<JournalArticle> JournalArticles => Set<JournalArticle>();
    public DbSet<LookbookLook> LookbookLooks => Set<LookbookLook>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KhazarDbContext).Assembly);
    }
}
