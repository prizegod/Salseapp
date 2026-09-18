using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using ShopSaleAPI.Data;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/database")] // 👈 நேரடி Route முகவரி
    public class DatabaseController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;
        private readonly IConfiguration _configuration;

        public DatabaseController(ShopSaleDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: api/database/info
        [HttpGet("info")]
        public IActionResult GetDatabaseInfo()
        {
            try
            {
                var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "ShopSale.db");
                var fileInfo = new FileInfo(dbPath);

                return Ok(new
                {
                    fileName = "ShopSale.db",
                    fileSize = fileInfo.Exists ? fileInfo.Length : 0,
                    activeConnection = true,
                    provider = "Microsoft.EntityFrameworkCore.Sqlite"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/database/tables/Products
        [HttpGet("tables/{tableName}")]
        public async Task<IActionResult> GetTableRecords(string tableName, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            try
            {
                var connString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ShopSale.db";
                using var connection = new SqliteConnection(connString);
                await connection.OpenAsync();

                var validTables = new List<string> { "Products", "Stores", "DailySales" };
                var targetTable = validTables.FirstOrDefault(t => t.Equals(tableName, StringComparison.OrdinalIgnoreCase));

                if (targetTable == null)
                {
                    return BadRequest(new { message = "Invalid table name requested." });
                }

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {targetTable} LIMIT @limit OFFSET @offset";
                command.Parameters.AddWithValue("@limit", limit);
                command.Parameters.AddWithValue("@offset", offset);

                using var reader = await command.ExecuteReaderAsync();
                var results = new List<Dictionary<string, object>>();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.GetValue(i);
                    }
                    results.Add(row);
                }

                return Ok(new
                {
                    tableName = targetTable,
                    totalCount = results.Count,
                    records = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}