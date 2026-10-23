using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocScannerController : ControllerBase
    {
        private readonly ILogger<DocScannerController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ShopSaleDbContext _dbContext;

        public DocScannerController(
            ILogger<DocScannerController> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ShopSaleDbContext dbContext)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _dbContext = dbContext;
        }

        [HttpPost("scan")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ScanDocument([FromForm] IFormFile? file)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No document file was uploaded. Please provide a receipt image or PDF." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"Unsupported file format '{extension}'. Allowed formats: JPG, PNG, WEBP, PDF." });
            }

            try
            {
                _logger.LogInformation("Processing receipt document via Gemini AI for UserId {UserId}: {FileName}", userId, file.FileName);

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                string base64Data = Convert.ToBase64String(fileBytes);

                string mimeType = file.ContentType;
                if (string.IsNullOrEmpty(mimeType) || mimeType == "application/octet-stream")
                {
                    mimeType = extension switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        ".pdf" => "application/pdf",
                        _ => "image/jpeg"
                    };
                }

                // Gemini AI-யிடமிருந்து மல்டிபிள் ஐட்டம்ஸ் கொண்ட JSON பெறுதல்
                string rawJsonResult = await ProcessAndGetRawGeminiJsonAsync(base64Data, mimeType);

                // இங்கு ஆட்டோ-சேவ் கிடையாது. டேட்டா நேரடியாக ஃபிரண்ட்-எண்டிற்கு அனுப்பப்படுகிறது.
                return Content(rawJsonResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while scanning document {FileName}", file.FileName);
                return StatusCode(500, new { message = "An error occurred while processing document with Gemini AI.", error = ex.Message });
            }
        }

        private async Task<string> ProcessAndGetRawGeminiJsonAsync(string base64Data, string mimeType)
        {
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
                            ?? _configuration["GEMINI_API_KEY"] 
                            ?? _configuration["GeminiApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return JsonSerializer.Serialize(new
                {
                    items = new[]
                    {
                        new { name = "Demo Item 1", price = 100.00 },
                        new { name = "Demo Item 2", price = 250.00 }
                    },
                    totalAmount = 350.00
                });
            }

            var httpClient = _httpClientFactory.CreateClient();
            var modelList = new[] { "gemini-3.5-flash", "gemini-3.5-pro", "gemini-3.6-flash", "gemini-3.8-flash" };

            string jsonResponseText = string.Empty;
            bool success = false;

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { inline_data = new { mime_type = mimeType, data = base64Data } },
                            new { text = "Extract all purchased items (name and price) and the total amount from this receipt image. Return strictly in JSON format with fields: items (array of objects with 'name' and 'price'), totalAmount." }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0,
                    responseMimeType = "application/json"
                }
            };

            foreach (var modelName in modelList)
            {
                try
                {
                    string requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                    var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody, jsonSerializerOptions), Encoding.UTF8, "application/json");
                    
                    var response = await httpClient.PostAsync(requestUrl, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        jsonResponseText = await response.Content.ReadAsStringAsync();
                        success = true;
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (!success || string.IsNullOrEmpty(jsonResponseText))
            {
                throw new Exception("All Gemini AI models are currently busy. Please try again later.");
            }

            using var doc = JsonDocument.Parse(jsonResponseText);
            string geminiOutputText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            return geminiOutputText.Replace("```json", "").Replace("```", "").Trim();
        }
    }
}