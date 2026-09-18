using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Models;

namespace ShopSaleAPI.Data
{
    public class ShopSaleDbContext : DbContext
    {
        public ShopSaleDbContext(DbContextOptions<ShopSaleDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Store> Stores => Set<Store>();
        public DbSet<DailySale> DailySales => Set<DailySale>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Relationships
            modelBuilder.Entity<DailySale>()
                .HasOne(s => s.Store)
                .WithMany(st => st.DailySales)
                .HasForeignKey(s => s.StoreID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DailySale>()
                .HasOne(s => s.Product)
                .WithMany(p => p.DailySales)
                .HasForeignKey(s => s.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            // Initial Seeding for default Store
            modelBuilder.Entity<Store>().HasData(
                new Store
                {
                    StoreID = 1,
                    Name = "Main Store",
                    Location = "Headquarters"
                },
                new Store
                {
                    StoreID = 2,
                    Name = "Downtown Outlet",
                    Location = "450 Metro Boulevard, Suite 12"
                }
            );

            // Initial Seeding for default Products
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Wireless Barcode Scanner 2D",
                    Category = "Hardware",
                    Price = 79.99m,
                    Stock = 45
                },
                new Product
                {
                    Id = 2,
                    Name = "Thermal Receipt Paper (50 Rolls)",
                    Category = "Supplies",
                    Price = 34.50m,
                    Stock = 120
                },
                new Product
                {
                    Id = 3,
                    Name = "Heavy Duty Cash Drawer RJ12",
                    Category = "Hardware",
                    Price = 119.00m,
                    Stock = 18
                },
                new Product
                {
                    Id = 4,
                    Name = "Touchscreen POS Terminal 15.6\"",
                    Category = "Systems",
                    Price = 549.00m,
                    Stock = 12
                },
                new Product
                {
                    Id = 5,
                    Name = "Direct Thermal Label Printer",
                    Category = "Hardware",
                    Price = 159.95m,
                    Stock = 28
                },
                new Product
                {
                    Id = 6,
                    Name = "POS Cable Organizer Sleeve Kit",
                    Category = "Accessories",
                    Price = 14.99m,
                    Stock = 85
                }
            );
        }
    }
}
