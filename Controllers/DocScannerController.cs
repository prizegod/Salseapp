using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopSaleAPI.Data; // உங்கள் DbContext Path-ஐ சரிபார்க்கவும்
using ShopSaleAPI.Models;

namespace ShopSaleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocScannerController : ControllerBase
    {
        private readonly ILogger<DocScannerController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ShopSaleDbContext _dbContext; // DbContext சேர்க்கப்பட்டது

        public DocScannerController(
            ILogger<DocScannerController> logger,
            IConfiguration _configuration,
            IHttpClientFactory httpClientFactory,
            ShopSaleDbContext dbContext)
        {
            _logger = logger;
            this._configuration = _configuration;
            _httpClientFactory = httpClientFactory;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Scans an uploaded receipt image or PDF, extracts structured fields using Gemini AI,
        /// and automatically saves non-existing products & sales entries into the Database.
        /// </summary>
        [HttpPost("scan")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(DocScanResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ScanDocument([FromForm] IFormFile? file)
        {
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
                _logger.LogInformation("Processing receipt document via Gemini AI: {FileName}, size: {Size} bytes", file.FileName, file.Length);

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

                // 1. Gemini AI மூலம் ஸ்கேன் செய்த தரவைப் பெறுதல்
                var result = await ProcessWithGeminiAsync(base64Data, mimeType);

                // 2. பெறப்பட்ட தரவை ஆட்டோமேட்டிக்காக டேட்டாபேஸில் சேமித்தல்
                await SaveScannedDataToDatabaseAsync(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while scanning document {FileName}", file.FileName);
                return StatusCode(500, new { message = "An error occurred while processing document with Gemini AI.", error = ex.Message });
            }
        }

        private async Task SaveScannedDataToDatabaseAsync(DocScanResult scanResult)
        {
            if (scanResult == null || scanResult.Items == null || !scanResult.Items.Any())
                return;

            // கடையின் ID (Default Store ID = 1 என எடுத்துக்கொள்ளப்படுகிறது)
            int defaultStoreId = 1;

            // ரசீது தேதி
            DateTime saleDate = DateTime.TryParse(scanResult.Date, out var parsedDate) ? parsedDate : DateTime.UtcNow;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in scanResult.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Description)) continue;

                    string itemName = item.Description.Trim();

                    // அ) பொருள் ஏற்கனவே Products Table-இல் இருக்கிறதா எனச் சரிபார்த்தல்
                    var existingProduct = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == itemName.ToLower());

                    int productId;

                    if (existingProduct != null)
                    {
                        productId = existingProduct.Id;
                    }
                    else
                    {
                        // ஆ) ஸ்டோரில் பொருள் இல்லை என்றால், தானாகப் புதிய பொருளாக Products Table-இல் சேமித்தல்
                        var newProduct = new Product
                        {
                            Name = itemName,
                            Category = "Scanned Bill Item",
                            Price = item.UnitPrice > 0 ? item.UnitPrice : item.LineTotal,
                            Stock = 0
                        };

                        _dbContext.Products.Add(newProduct);
                        await _dbContext.SaveChangesAsync(); // புதிய Product ID உருவாகச் சேமிக்கப்படுகிறது

                        productId = newProduct.Id;
                    }

                    // இ) DailySales Table-இல் விற்பனையைப் பதிவு செய்தல்
                    var dailySalesEntry = new DailySale
                    {
                        StoreID = defaultStoreId,
                        ProductID = productId,
                        QuantitySold = item.Quantity > 0 ? item.Quantity : 1,
                        TotalAmount = item.LineTotal > 0 ? item.LineTotal : (item.Quantity * item.UnitPrice),
                        SaleDate = saleDate
                    };

                    _dbContext.DailySales.Add(dailySalesEntry);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Scanned receipt items saved automatically into the database.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to auto-save scanned items to database.");
                throw;
            }
        }

        private async Task<DocScanResult> ProcessWithGeminiAsync(string base64Data, string mimeType)
        {
            string apiKey = _configuration["GEMINI_API_KEY"] ?? _configuration["GeminiApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Gemini API Key is not configured in Environment Variables (GEMINI_API_KEY).");
            }

            var httpClient = _httpClientFactory.CreateClient();
            string requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={apiKey}";
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { inlineData = new { mimeType = mimeType, data = base64Data } },
                            new { text = "Extract store/merchant name, receipt/invoice number, date (YYYY-MM-DD), list of items with description, quantity, unit price, line total, tax amount, and final total amount from this receipt image." }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            merchantName = new { type = "STRING" },
                            receiptNo = new { type = "STRING" },
                            date = new { type = "STRING" },
                            totalAmount = new { type = "NUMBER" },
                            taxAmount = new { type = "STRING" },
                            paymentMethod = new { type = "STRING" },
                            rawText = new { type = "STRING", description = "Transcribed handwritten text lines" },
                            items = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        description = new { type = "STRING" },
                                        quantity = new { type = "NUMBER" },
                                        unitPrice = new { type = "NUMBER" },
                                        lineTotal = new { type = "NUMBER" }
                                    },
                                    required = new[] { "description", "lineTotal" }
                                }
                            }
                        },
                        required = new[] { "merchantName", "totalAmount", "items" }
                    }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(requestUrl, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error ({response.StatusCode}): {errorResponse}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            string geminiOutputText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var extractedData = JsonSerializer.Deserialize<GeminiExtractedResponse>(geminiOutputText, options);

            return new DocScanResult
            {
                Success = true,
                ReceiptNo = extractedData?.ReceiptNo ?? "N/A",
                TotalAmount = extractedData?.TotalAmount ?? 0m,
                Date = extractedData?.Date ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
                MerchantName = extractedData?.MerchantName ?? "Unknown Store",
                TaxAmount = extractedData?.TaxAmount ?? "0.00",
                PaymentMethod = extractedData?.PaymentMethod ?? "Cash",
                Items = extractedData?.Items?.Select(i => new DocScanItem
                {
                    Description = i.Description,
                    Quantity = i.Quantity > 0 ? (int)i.Quantity : 1,
                                                     UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : i.LineTotal,
                                                     LineTotal = i.LineTotal
                }).ToList() ?? new List<DocScanItem>(),
                RawText = !string.IsNullOrEmpty(extractedData?.RawText) ? extractedData.RawText : "Processed via Gemini 3.6 Flash",
                ConfidenceScore = 0.98,
                ProcessedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        private class GeminiExtractedResponse
        {
            public string? MerchantName { get; set; }
            public string? ReceiptNo { get; set; }
            public string? Date { get; set; }
            public decimal TotalAmount { get; set; }
            public string? TaxAmount { get; set; }
            public string? PaymentMethod { get; set; }
            public string? RawText { get; set; }
            public List<GeminiItem>? Items { get; set; }
        }

        private class GeminiItem
        {
            public string Description { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
        }
    }
}
