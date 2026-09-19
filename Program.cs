using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    // Avoid circular reference loop issues with EF navigation properties
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// HttpClient Service Registration (DocScannerController-க்குத் தேவைப்படுகிறது)
builder.Services.AddHttpClient();

// 2. Configure CORS policy to ALLOW ALL ORIGINS
var corsPolicyName = "AllowMobileAndWeb";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Mobile/Capacitor origins-க்கு சிறந்தது
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// 3. Configure EF Core with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
?? "Data Source=ShopSale.db";

builder.Services.AddDbContext<ShopSaleDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// 4. Configure Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() {
        Title = "ShopSale Management System API",
        Version = "v1",
        Description = "ASP.NET Core 6.0 Web API with SQLite & Entity Framework Core for ShopSale Management"
    });
});

var app = builder.Build();

// 5. Automatic database creation and initial seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ShopSaleDbContext>();
        // Ensure database and tables are created with default seed data
        context.Database.EnsureCreated();
        app.Logger.LogInformation("SQLite database successfully initialized and verified.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while creating or seeding the SQLite database.");
    }
}

// 6. Configure the HTTP request pipeline (Always Enable Swagger)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShopSale API v1");
    c.RoutePrefix = "swagger"; // http://localhost:5000/swagger
});

// 1. Routing
app.UseRouting();

// 2. CORS (Routing-க்கு பின் கட்டாயம் இருக்க வேண்டும்)
app.UseCors(corsPolicyName);

// 3. Authorization & Controllers
app.UseAuthorization();

app.MapControllers();

app.Run();
