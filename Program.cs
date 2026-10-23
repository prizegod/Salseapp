using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using DotNetEnv;

// 🎯 1. .env கோப்பை API தொடங்கும் முதல் வரியிலேயே Load செய்ய வேண்டும்
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// 2. Add Services & Controller Options
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddHttpClient();

// 3. Configure CORS Policy
var corsPolicyName = "AllowMobileAndWeb";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow all origins for dev
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Axios withCredentials: true
    });
});

// 4. JWT Configuration & Authentication
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_KEY") 
                   ?? builder.Configuration["Jwt:Key"] 
                   ?? "ShopShail_Super_Secret_Key_2026_For_Security_Purpose";

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                ?? builder.Configuration["Jwt:Issuer"] 
                ?? "ShopShailAPI";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                 ?? builder.Configuration["Jwt:Audience"] 
                 ?? "ShopShailClient";

var key = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        // 🎯 🔑 மிக முக்கியமானது: JWT-இல் உள்ள Role-ஐ C# [Authorize(Roles="Admin")] சரியாகப் படிக்க
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

// 5. Configure EF Core with Supabase PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ShopSaleDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});
// 6. Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ShopSale Management System API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter JWT Token format: 'Bearer <your_token>'"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// 7. Global Exception Handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "Internal Server Error",
            message = exception?.Message,
            innerError = exception?.InnerException?.Message
        });

        await context.Response.WriteAsync(result);
    });
});

// 🎯 8. DB Initialization & Default Admin Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ShopSaleDbContext>();
        context.Database.EnsureCreated();
        
        var adminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? "admin";
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "admin123";

        // 🔑 Admin பயனர் இல்லையெனில் BCrypt Password-உடன் உருவாக்குதல்
        var existingAdmin = context.Users.FirstOrDefault(u => u.Username.ToLower() == adminUsername.ToLower());
        if (existingAdmin == null)
        {
            var defaultAdmin = new User
            {
                Username = adminUsername,
                Password = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin"
            };
            context.Users.Add(defaultAdmin);
            context.SaveChanges();
            app.Logger.LogInformation("Default Admin user seeded into Database.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error initializing SQLite DB or seeding Admin.");
    }
}

// 9. HTTP Request Pipeline Order
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShopSale API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

// 🎯 Pipeline Order: CORS -> Authentication -> Authorization
app.UseCors(corsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();