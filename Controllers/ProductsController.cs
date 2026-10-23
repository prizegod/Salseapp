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
    [Authorize] // 🔐 லாகின் செய்த அங்கீகரிக்கப்பட்ட பயனர்களுக்கு மட்டும்
    public class ProductsController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public ProductsController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // GET: api/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] int? storeId)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized();
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var query = _context.Products.AsQueryable();

                // 🎯 1. Query Parameter-ல் storeId வந்தால் பில்டர் செய்யப்படுகிறது
                if (storeId.HasValue && storeId.Value > 0)
                {
                    query = query.Where(p => p.StoreId == storeId.Value);
                }
                // 🎯 2. Admin தவிர மற்ற பயனர்களுக்கு அவரவர் Store/User பொருட்களுக்கு மட்டும் பில்டர் செய்யப்படுகிறது
                else if (userRole != "Admin")
                {
                    query = query.Where(p => p.StoreId == userId);
                }

                var products = await query.OrderBy(p => p.Name).ToListAsync();
                return Ok(products ?? new List<Product>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);

                if (product == null)
                {
                    return NotFound(new { message = $"Product with ID {id} not found." });
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // POST: api/products
        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized();
                }

                if (product == null || string.IsNullOrWhiteSpace(product.Name))
                {
                    return BadRequest(new { message = "Product name is required." });
                }

                // 🎯 பொருளுக்கு StoreId குறிப்பிடப்படவில்லை எனில் லாகின் செய்த பயனர் ID-யை StoreId-ஆக அமைக்கிறது
                if (product.StoreId <= 0)
                {
                    product.StoreId = userId;
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product product)
        {
            try
            {
                if (id != product.Id)
                {
                    return BadRequest(new { message = "Product ID mismatch." });
                }

                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null)
                {
                    return NotFound(new { message = $"Product with ID {id} not found." });
                }

                existingProduct.Name = product.Name;
                existingProduct.Category = product.Category;
                existingProduct.Price = product.Price;
                existingProduct.Stock = product.Stock;

                await _context.SaveChangesAsync();
                return Ok(existingProduct);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return NotFound(new { message = $"Product with ID {id} not found." });
                }

                var hasSales = await _context.DailySales.AnyAsync(s => s.ProductID == id);
                if (hasSales)
                {
                    return BadRequest(new { message = "Cannot delete product because it has associated sales records." });
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Product deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}