using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public SalesController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // GET: api/sales
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSales()
        {
            var sales = await _context.DailySales
                .Include(s => s.Product)
                .Include(s => s.Store)
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new
                {
                    s.SaleID,
                    s.StoreID,
                    StoreName = s.Store != null ? s.Store.Name : "Unknown Store",
                    s.ProductID,
                    ProductName = s.Product != null ? s.Product.Name : "Unknown Product",
                    ProductCategory = s.Product != null ? s.Product.Category : "N/A",
                    ProductUnitPrice = s.Product != null ? s.Product.Price : 0m,
                    s.QuantitySold,
                    s.TotalAmount,
                    s.SaleDate
                })
                .ToListAsync();

            return Ok(sales);
        }

        // POST: api/sales
        [HttpPost]
        public async Task<ActionResult<object>> RecordSale([FromBody] CreateSaleDto dto)
        {
            if (dto.QuantitySold <= 0)
            {
                return BadRequest(new { message = "Quantity sold must be greater than zero." });
            }

            var product = await _context.Products.FindAsync(dto.ProductID);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {dto.ProductID} does not exist." });
            }

            var store = await _context.Stores.FindAsync(dto.StoreID);
            if (store == null)
            {
                return NotFound(new { message = $"Store with ID {dto.StoreID} does not exist." });
            }

            if (product.Stock < dto.QuantitySold)
            {
                return BadRequest(new
                {
                    message = $"Insufficient stock for {product.Name}. Available: {product.Stock}, Requested: {dto.QuantitySold}"
                });
            }

            // Deduct stock
            product.Stock -= dto.QuantitySold;

            // Calculate total amount
            decimal totalAmount = Math.Round(product.Price * dto.QuantitySold, 2);

            var sale = new DailySale
            {
                StoreID = dto.StoreID,
                ProductID = dto.ProductID,
                QuantitySold = dto.QuantitySold,
                TotalAmount = totalAmount,
                SaleDate = dto.SaleDate ?? DateTime.UtcNow
            };

            _context.DailySales.Add(sale);
            await _context.SaveChangesAsync();

            var response = new
            {
                sale.SaleID,
                sale.StoreID,
                StoreName = store.Name,
                sale.ProductID,
                ProductName = product.Name,
                ProductCategory = product.Category,
                ProductUnitPrice = product.Price,
                sale.QuantitySold,
                sale.TotalAmount,
                sale.SaleDate,
                RemainingStock = product.Stock
            };

            return CreatedAtAction(nameof(GetSales), new { id = sale.SaleID }, response);
        }
    }
}
