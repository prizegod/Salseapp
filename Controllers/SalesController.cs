using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace ShopSaleAPI.Controllers
{
    public class ScannedItemDto
    {
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    public class CreateSaleRequestDto
    {
        [JsonPropertyName("storeId")]
        public int StoreId { get; set; }

        [JsonPropertyName("items")]
        public List<ScannedItemDto> Items { get; set; } = new();

        [JsonPropertyName("totalAmount")]
        public decimal TotalAmount { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public SalesController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // 1. Get Sales with Date/Store Filtering
        [HttpGet]
        public async Task<IActionResult> GetSales([FromQuery] int? storeId, [FromQuery] int? queryUserId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int tokenUserId))
            {
                return Unauthorized();
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var query = _context.Set<DailySale>().AsQueryable();

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(s => s.StoreID == storeId.Value);
            }
            else if (queryUserId.HasValue && queryUserId.Value > 0)
            {
                query = query.Where(s => s.UserId == queryUserId.Value);
            }
            else if (userRole == "Agent")
            {
                query = query.Where(s => s.UserId == tokenUserId);
            }
            else if (userRole == "Manager" || userRole == "User")
            {
                query = query.Where(s => s.UserId == tokenUserId || s.StoreID == tokenUserId);
            }

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();
            return Ok(sales);
        }

        // 2. Summary Endpoint for Dashboard (Auto-Resets daily - Invoice count & Decimal Sum fix)
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryReport([FromQuery] int? storeId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid User ID" });
            }

            try
            {
                var today = DateTime.UtcNow.Date;
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var query = _context.Set<DailySale>()
                    .Where(s => s.SaleDate.Date == today);

                if (storeId.HasValue && storeId.Value > 0)
                {
                    query = query.Where(s => s.StoreID == storeId.Value);
                }
                else if (userRole != "Admin")
                {
                    query = query.Where(s => s.UserId == userId || s.StoreID == userId);
                }

                var salesList = await query.ToListAsync();
                
                decimal todaySales = salesList.Sum(s => s.TotalAmount);
                int totalOrders = salesList.Any() ? salesList.GroupBy(s => s.SaleDate).Count() : 0;

                int totalProducts = 0;
                try
                {
                    totalProducts = await _context.Set<Product>().CountAsync();
                }
                catch
                {
                    totalProducts = 0;
                }

                return Ok(new
                {
                    todaySales = todaySales,
                    totalProducts = totalProducts,
                    totalOrders = totalOrders
                });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"--- SUMMARY ERROR 500: {innerMsg} ---");
                return Ok(new { todaySales = 0, totalProducts = 0, totalOrders = 0 });
            }
        }

        // 3. Create Sale (Safe Database Insertion)
        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequestDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid User ID Token" });
            }
        
            if (request == null || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "விற்பனைப் பொருட்கள் எதுவும் கிடைக்கவில்லை." });
            }
        
            try
            {
                var salesToInsert = new List<DailySale>();
        
                foreach (var item in request.Items)
                {
                    var itemQty = item.Quantity > 0 ? item.Quantity : 1;
                    var itemTotal = item.Price * itemQty;
        
                    var sale = new DailySale
                    {
                        UserId = userId,
                        StoreID = request.StoreId > 0 ? request.StoreId : userId,
                        ProductID = item.ProductId > 0 ? item.ProductId : 0,
                        ProductName = !string.IsNullOrEmpty(item.Name) ? item.Name : "Product",
                        QuantitySold = itemQty,
                        TotalAmount = itemTotal > 0 ? itemTotal : item.Price,
                        SaleDate = DateTime.UtcNow
                    };
        
                    salesToInsert.Add(sale);
                }
        
                await _context.Set<DailySale>().AddRangeAsync(salesToInsert);
                await _context.SaveChangesAsync();
        
                return Ok(new { success = true, count = salesToInsert.Count, data = salesToInsert });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"--- CREATE SALE 500 ERROR: {innerMessage} ---");
                return StatusCode(500, new { message = innerMessage });
            }
        }
    }
}