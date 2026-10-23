using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoresController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public StoresController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // GET: api/stores
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Store>>> GetStores()
        {
            return await _context.Stores.OrderBy(s => s.StoreID).ToListAsync();
        }

        // GET: api/stores/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Store>> GetStore(int id)
        {
            var store = await _context.Stores.FindAsync(id);

            if (store == null)
            {
                return NotFound(new { message = $"Store with ID {id} not found." });
            }

            return store;
        }

        // POST: api/stores
        [HttpPost]
        public async Task<ActionResult<Store>> CreateStore(Store store)
        {
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStore), new { id = store.StoreID }, store);
        }
    }
}
