using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using LinkedInAI.API.Data;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;

// ─── Serilog bootstrap ───────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day));

    // ─── Database ──────────────────────────────────────────────────────────────
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not configured");

    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseMySql(connStr, ServerVersion.AutoDetect(connStr),
            mysql => mysql.EnableRetryOnFailure(3)));

    // ─── Redis ─────────────────────────────────────────────────────────────────
    var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisConn));
    builder.Services.AddScoped<IDatabase>(sp =>
        sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

    // ─── RabbitMQ ──────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<IConnection>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var factory = new ConnectionFactory
        {
            HostName = cfg["RabbitMQ:Host"] ?? "localhost",
            Port = int.TryParse(cfg["RabbitMQ:Port"], out var p) ? p : 5672,
            UserName = cfg["RabbitMQ:User"] ?? "guest",
            Password = cfg["RabbitMQ:Password"] ?? "guest",
            VirtualHost = cfg["RabbitMQ:VHost"] ?? "/",
            AutomaticRecoveryEnabled = true
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    });

    // ─── Application Services ──────────────────────────────────────────────────
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IProfileService, ProfileService>();
    builder.Services.AddScoped<ILinkedInScraperService, LinkedInScraperService>();
    builder.Services.AddSingleton<ScrapeProgressStore>();
    builder.Services.AddScoped<IAnalysisService, AnalysisService>();
    builder.Services.AddScoped<IResumeService, ResumeService>();
    builder.Services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();

    // OAuth services (keyed for DI)
    builder.Services.AddKeyedScoped<IOAuthService, GoogleOAuthService>("google");
    builder.Services.AddKeyedScoped<IOAuthService, LinkedInOAuthService>("linkedin");

    // Background consumers for async results
    builder.Services.AddHostedService<AnalysisResultConsumer>();
    builder.Services.AddHostedService<ResumeParseResultConsumer>();

    builder.Services.AddHttpClient();

    // ─── FluentValidation ──────────────────────────────────────────────────────
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

    // ─── JWT Authentication ────────────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key not configured");
    if (jwtKey.Length < 32)
        throw new InvalidOperationException("Jwt:Key must be at least 32 characters");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

    builder.Services.AddAuthorization(opts =>
        opts.AddPolicy("AdminOnly", p => p.RequireClaim("isAdmin", "true")));

    // ─── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", p =>
        p.WithOrigins("http://localhost:3000", "http://localhost:3001",
                      "http://localhost:5173", "https://localhost:3000",
                      "https://localhost:3001", "https://localhost:5173")
         .AllowAnyHeader().AllowAnyMethod()));

    // ─── Controllers + Swagger ─────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ─── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Migrate Database ─────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (app.Environment.IsEnvironment("Test"))
            db.Database.EnsureCreated();
        else
            db.Database.Migrate();
    }

    // ─── Middleware Pipeline ───────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
       .AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for test factories
public partial class Program { }
