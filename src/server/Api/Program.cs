using System.Text;
using ERP.Api.Swagger;
using ERP.Application;
using ERP.Application.Admissions.Options;
using ERP.Domain.Admissions.Entities;
using ERP.Infrastructure;
using ERP.Infrastructure.Authentication;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

builder.Services.Configure<AdmissionsWorkflowOptions>(
    builder.Configuration.GetSection(AdmissionsWorkflowOptions.SectionName));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure DateOnly serialization to use ISO 8601 format (YYYY-MM-DD)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // DateOnly is automatically serialized as string in .NET 8, but we ensure proper format
    });
builder.Services.AddEndpointsApiExplorer();

// Add custom API description provider to handle [FromForm] IFormFile parameters
// This runs before Swashbuckle tries to generate parameters, preventing errors
builder.Services.AddSingleton<IApiDescriptionProvider, FileUploadApiDescriptionProvider>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ERP API",
        Version = "v1",
        Description = "ERP System API"
    });
    
    // Use full namespace-qualified type names to avoid schema ID conflicts
    // This ensures types with the same name in different namespaces get unique schema IDs
    // Example: DocumentVerificationStatusDto exists in two namespaces and needs unique IDs
    options.CustomSchemaIds(type =>
    {
        if (string.IsNullOrEmpty(type.FullName))
        {
            return type.Name;
        }
        
        // Use full namespace path to ensure uniqueness
        // Replace nested type separator (+) with dot, and replace dots with underscores for valid schema IDs
        var schemaId = type.FullName
            .Replace("+", ".")
            .Replace(".", "_")
            .Replace("`", "_");
        
        // For generic types, include type arguments
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var genericArgs = type.GetGenericArguments();
            var argsString = string.Join("_", genericArgs.Select(t => 
                t.FullName?.Replace("+", ".").Replace(".", "_").Replace("`", "_") ?? t.Name));
            schemaId = $"{schemaId}_{argsString}";
        }
        
        return schemaId;
    });
    
    // Handle file uploads - Map IFormFile to a proper OpenAPI schema
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
    
    // Configure schema filter for IFormFile
    options.SchemaFilter<FileUploadSchemaFilter>();
    
    // Configure file upload operations
    options.OperationFilter<FileUploadOperationFilter>();
    
    // Configure parameter filter to handle [FromForm] IFormFile parameters
    options.ParameterFilter<FileUploadParameterFilter>();
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var secret = builder.Configuration["Authentication:Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT secret is not configured.");
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        if (secretBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:Secret must be at least 32 bytes when UTF-8 encoded (HS256 requires a key longer than 256 bits).");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Authentication:Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Authentication:Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Configure HTTPS redirection options
var httpsPort = builder.Configuration["HTTPS_PORT"];
if (!string.IsNullOrEmpty(httpsPort) && int.TryParse(httpsPort, out var port))
{
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.HttpsPort = port;
    });
}

var app = builder.Build();

if (app.Environment.IsProduction())
{
    var productionOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                           ?? Array.Empty<string>();
    if (productionOrigins.Length == 0 || productionOrigins.All(string.IsNullOrWhiteSpace))
    {
        throw new InvalidOperationException(
            "Production requires Cors:AllowedOrigins (non-empty). Open CORS is not allowed when ASPNETCORE_ENVIRONMENT=Production.");
    }
}

// Production: apply EF migrations on startup (avoids manual dotnet ef against remote DB).
// Local/dev: continue using `dotnet ef database update` against your machine.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Environment.IsProduction())
    {
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            throw;
        }
    }

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
    try
    {
        await SeedAdminUser.SeedOrUpdateAsync(context, passwordHasher);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding admin user");
    }
}

// Must run first when behind nginx/Traefik so scheme/host match the public URL (fixes HTTPS redirect + correct request context).
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP API v1");
        c.RoutePrefix = "swagger";
    });
    // Disable HTTPS redirection in development to allow HTTP connections from frontend
    // app.UseHttpsRedirection();
}
else
{
    // Use HTTPS redirection in production
    app.UseHttpsRedirection();
}

app.UseCors("DefaultCorsPolicy");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: use GET /api/health when nginx only proxies /api/* to this app (GET /health at site root is served by the SPA).
app.MapGet("/health", () => Results.Text("ok", "text/plain"));
app.MapGet("/api/health", () => Results.Text("ok", "text/plain"));

if (app.Environment.IsDevelopment())
{
    // Root has no page; browsers expecting a UI get Swagger instead of 404.
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.Run();

public partial class Program;
