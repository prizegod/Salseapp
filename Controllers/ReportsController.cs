using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public ReportsController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // GET: api/reports/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                // டேபிளில் டேட்டா உள்ளதா எனச் சரிபார்த்தல்
                var salesList = await _context.DailySales.ToListAsync();

                if (salesList == null || !salesList.Any())
                {
                    return Ok(new
                    {
                        totalRevenue = 0,
                        totalUnitsSold = 0,
                        totalSalesCount = 0
                    });
                }

                // மெமரியிலேயே கணக்கிடுதல் (SQL Translation பிழைகளைத் தவிர்க்க)
                var totalRevenue = salesList.Sum(s => s.TotalAmount);
                var totalUnitsSold = salesList.Sum(s => s.QuantitySold);
                var salesCount = salesList.Count;

                return Ok(new
                {
                    totalRevenue = totalRevenue,
                    totalUnitsSold = totalUnitsSold,
                    totalSalesCount = salesCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        // GET: api/reports/daily-sales
        // GET: api/reports/daily-sales
        [HttpGet("daily-sales")]
        public async Task<IActionResult> GetDailySales()
        {
            try
            {
                // .Include தேவையில்லாமல் நேரடியாக DailySales டேபிளை மட்டும் க்வெரி செய்வதால் Join பிழைகளைத் தவிர்க்கலாம்
                var sales = await _context.DailySales
                .OrderBy(s => s.SaleDate)
                .ToListAsync();

                if (sales == null || !sales.Any())
                {
                    return Ok(new List<object>());
                }

                var grouped = sales
                .GroupBy(s => s.SaleDate.ToString("yyyy-MM-dd"))
                .Select(g => new
                {
                    date = g.Key,
                    count = g.Count(),
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
