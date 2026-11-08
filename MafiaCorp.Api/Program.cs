using MafiaCorp.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Setup Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MafiaCorp API", Version = "v1" });

    // Enable JWT Authentication in Swagger UI
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your valid JWT token below."
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

// Setup Database Connection String (checks all common env vars on Render/Heroku/Docker)
var rawConnectionString = 
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["DefaultConnection"]
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? builder.Configuration["POSTGRES_URL"]
    ?? builder.Configuration["POSTGRESQL_URL"];

var resolvedConnectionString = Program.BuildNpgsqlConnectionString(rawConnectionString);

try
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(resolvedConnectionString);
    Console.WriteLine($"[Database] Target Host={csb.Host};Port={csb.Port};Database={csb.Database};Username={csb.Username};SSL={csb.SslMode}");
}
catch
{
    Console.WriteLine("[Database] Configured with custom connection string.");
}

builder.Services.AddDbContext<MafiaDbContext>(options =>
    options.UseNpgsql(resolvedConnectionString));

// Setup Authentication
var jwtKey = builder.Configuration["Jwt:Key"] 
             ?? builder.Configuration["Jwt__Key"] 
             ?? "SuperSecretKeyForMafiaCorpThatIsLongEnoughToNotThrowExceptions123!";
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // For local and container reverse proxies
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Jwt__Issuer"] ?? "MafiaCorpApi",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Jwt__Audience"] ?? "MafiaCorpClients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

// Show developer exception page in production to avoid silent empty 500 errors
app.UseDeveloperExceptionPage();

// Automatically apply EF Core database migrations on startup if connected
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MafiaDbContext>();
    try
    {
        Console.WriteLine("[Database] Checking connection...");
        if (db.Database.CanConnect())
        {
            Console.WriteLine("[Database] Successfully connected! Applying migrations...");
            try
            {
                db.Database.Migrate();
                Console.WriteLine("[Database] Migrations applied successfully.");
            }
            catch (Exception migEx)
            {
                Console.WriteLine($"[Database Migration Warning] Migrate failed: {migEx.Message}. Attempting EnsureCreated()...");
                db.Database.EnsureCreated();
                Console.WriteLine("[Database] EnsureCreated() completed successfully.");
            }
        }
        else
        {
            Console.WriteLine("[Database Warning] Database.CanConnect() returned false. Database may be unreachable or offline.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Error] Initialization failed: {ex.Message}");
    }
}

// Enable Swagger UI across all environments for easy testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MafiaCorp API v1");
});

app.UseAuthentication();
app.UseAuthorization();

// Database Health & Diagnostic Endpoint
app.MapGet("/api/health/db", async (MafiaDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        string? host = "Unknown";
        string? database = "Unknown";
        string? ssl = "Unknown";
        try
        {
            var csb = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            host = csb.Host;
            database = csb.Database;
            ssl = csb.SslMode.ToString();
        }
        catch { }

        int userCount = -1;
        if (canConnect)
        {
            userCount = await db.Users.CountAsync();
        }

        return Results.Ok(new
        {
            status = canConnect ? "connected" : "cannot_connect",
            canConnect,
            host,
            database,
            ssl,
            userCount
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "error",
            error = ex.Message,
            inner = ex.InnerException?.Message,
            stack = ex.StackTrace
        }, statusCode: 500);
    }
});

app.MapControllers();

app.Run();

public partial class Program 
{
    public static string BuildNpgsqlConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Host=localhost;Database=MafiaCorpDB;Username=postgres;Password=123";
        }

        raw = raw.Trim('"', '\'', ' ', '\t', '\r', '\n');

        // Check if it's a URI format (postgres:// or postgresql://)
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':', 2);
                var username = Uri.UnescapeDataString(userInfo[0]);
                var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                var builder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                    SslMode = Npgsql.SslMode.Require
                };

                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database Warning] Could not parse URI connection string: {ex.Message}. Falling back to raw string.");
            }
        }

        // Key-value connection string (e.g. Host=...;Database=...)
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(raw);
            if (!string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(builder.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Npgsql.SslMode.Require;
            }
            return builder.ConnectionString;
        }
        catch
        {
            return raw;
        }
    }
}
