using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;
using System.Security.Claims;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public ReportsController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // GET: api/reports/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? storeId)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized();
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var query = _context.DailySales.AsQueryable();

                if (storeId.HasValue && storeId.Value > 0)
                {
                    query = query.Where(s => s.StoreID == storeId.Value || s.UserId == storeId.Value);
                }
                else if (userRole != "Admin")
                {
                    query = query.Where(s => s.UserId == userId || s.StoreID == userId);
                }

                var salesList = await query.ToListAsync();

                if (salesList == null || !salesList.Any())
                {
                    return Ok(new
                    {
                        totalRevenue = 0,
                        totalUnitsSold = 0,
                        totalSalesCount = 0
                    });
                }

                var totalRevenue = salesList.Sum(s => s.TotalAmount);
                var totalUnitsSold = salesList.Sum(s => s.QuantitySold);
                // ஒவ்வொரு தனித்தனி பில்லையும் (SaleID) கணக்கிடுதல்
                var totalOrders = salesList.Select(s => s.SaleID).Distinct().Count();

                return Ok(new
                {
                    totalRevenue = totalRevenue,
                    totalUnitsSold = totalUnitsSold,
                    totalSalesCount = totalOrders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/reports/recent-sales (ஒவ்வொரு சேலையும் தனித்தனி இன்வாய்ஸாகக் காட்டுதல்)
        [HttpGet("recent-sales")]
        public async Task<IActionResult> GetRecentSales([FromQuery] int? storeId)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized();
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var query = _context.DailySales.AsQueryable();

                if (storeId.HasValue && storeId.Value > 0)
                {
                    query = query.Where(s => s.StoreID == storeId.Value || s.UserId == storeId.Value);
                }
                else if (userRole != "Admin")
                {
                    query = query.Where(s => s.UserId == userId || s.StoreID == userId);
                }

                // ஒவ்வொரு தனிப்பட்ட விற்பனைப் பதிவையும் (SaleID) வரிசையாகப் பெறுதல்
                var salesRecords = await query
                    .OrderByDescending(s => s.SaleDate)
                    .ThenByDescending(s => s.SaleID)
                    .Take(30)
                    .Select(s => new
                    {
                        id = $"INV-{1000 + s.SaleID}",
                        date = s.SaleDate.ToString("yyyy-MM-ddTHH:mm:ssZ"), // பிரண்ட்-எண்ட் சரியாகப் படிக்க ISO பார்மட்
                        itemsCount = s.QuantitySold,
                        totalAmount = s.TotalAmount,
                        paymentMode = "UPI / Cash"
                    })
                    .ToListAsync();

                return Ok(salesRecords);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/reports/daily-sales
        [HttpGet("daily-sales")]
        public async Task<IActionResult> GetDailySales([FromQuery] int? storeId)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized();
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var query = _context.DailySales.AsQueryable();

                if (storeId.HasValue && storeId.Value > 0)
                {
                    query = query.Where(s => s.StoreID == storeId.Value || s.UserId == storeId.Value);
                }
                else if (userRole != "Admin")
                {
                    query = query.Where(s => s.UserId == userId || s.StoreID == userId);
                }

                var sales = await query.OrderBy(s => s.SaleDate).ToListAsync();

                if (sales == null || !sales.Any())
                {
                    return Ok(new List<object>());
                }

                var grouped = sales
                    .GroupBy(s => s.SaleDate.ToString("yyyy-MM-dd"))
                    .Select(g => new
                    {
                        date = g.Key,
                        count = g.Select(x => x.SaleID).Distinct().Count(),
                        unitsSold = g.Sum(x => x.QuantitySold),
                        totalSales = g.Sum(x => x.TotalAmount)
                    })
                    .OrderByDescending(x => x.date)
                    .Take(30)
                    .ToList();

                return Ok(grouped);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}