using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderService.Api.Middleware;
using OrderService.Application.DTOs;
using OrderService.Application.Security;
using OrderService.Application.Services;
using OrderService.Application.Validators;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (DB, repositórios, UoW, RLS, JWT token) ───────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application services (casos de uso) ─────────────────────────────────────
builder.Services.AddScoped<CreateOrderService>();
builder.Services.AddScoped<ConfirmOrderService>();
builder.Services.AddScoped<CancelOrderService>();
builder.Services.AddScoped<GetOrderService>();
builder.Services.AddScoped<ListOrdersService>();

// ── Refresh token services ────────────────────────────────────────────────────
var refreshDays = builder.Configuration.GetSection("Jwt").GetValue("RefreshTokenExpirationDays", 7);
builder.Services.AddScoped<IRefreshTokenIssuer>(sp =>
    new RefreshTokenIssuer(
        sp.GetRequiredService<IRefreshTokenRepository>(),
        sp.GetRequiredService<IJwtTokenService>(),
        sp.GetRequiredService<IUnitOfWork>(),
        refreshDays));

builder.Services.AddScoped<RefreshTokenService>(sp =>
    new RefreshTokenService(
        sp.GetRequiredService<IRefreshTokenRepository>(),
        sp.GetRequiredService<IJwtTokenService>(),
        sp.GetRequiredService<IUnitOfWork>(),
        refreshDays));

builder.Services.AddScoped<LogoutService>();

// ── Validators (FluentValidation) ────────────────────────────────────────────
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("JWT Key não configurada. Defina 'Jwt:Key' nas variáveis de ambiente.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Desabilita remapeamento automático de claim types (ex: "role" → ClaimTypes.Role),
        // garantindo que User.IsInRole("Admin") e FindFirstValue("role") funcionem corretamente.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                var body = """{"status":401,"title":"Não autenticado","detail":"Token JWT ausente ou inválido. Obtenha um token em POST /auth/token e envie no header: Authorization: Bearer {token}"}""";
                return ctx.Response.WriteAsync(body);
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/problem+json";
                var body = """{"status":403,"title":"Acesso não autorizado","detail":"Você não tem permissão para acessar este recurso."}""";
                return ctx.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting (proteção contra abuso) ────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política global: 100 req/min por IP; sem IP (testes/loopback) → sem limite
    options.AddPolicy("global", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return ip is null
            ? RateLimitPartition.GetNoLimiter("no-ip")
            : RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Política para auth: mais restritiva (10 req/min por IP) para dificultar brute-force;
    // sem IP (testes/loopback) → sem limite
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return ip is null
            ? RateLimitPartition.GetNoLimiter("no-ip")
            : RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsync(
            """{"status":429,"title":"Muitas requisições","detail":"Você excedeu o limite de requisições. Aguarde um momento antes de tentar novamente."}""",
            cancellationToken);
    };
});

// ── ProblemDetails + Exception handler ───────────────────────────────────────
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── HSTS ──────────────────────────────────────────────────────────────────────
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// ── Swagger com suporte a JWT ─────────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Service API",
        Version = "v1",
        Description = "API REST de Pedidos — teste técnico .NET Sênior. " +
                      "Obtenha um token JWT em POST /auth/token e use no botão 'Authorize'."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT obtido em POST /auth/token. Exemplo: Bearer eyJhbG..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ── Pipeline de middlewares ───────────────────────────────────────────────────

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

app.UseHttpsRedirection();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1");
        options.DisplayRequestDuration();
    });
}

app.UseAuthentication();

// Verifica blacklist de JTIs após autenticação — rejeita tokens revogados por logout
app.UseMiddleware<TokenBlacklistMiddleware>();

app.UseAuthorization();

app.MapControllers()
   .RequireRateLimiting("global");

// Rota de auth com política mais restritiva
app.MapControllerRoute(
    name: "auth",
    pattern: "auth/{action}",
    defaults: new { controller = "Auth" })
   .RequireRateLimiting("auth");

app.MapHealthChecks("/health");

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program { }
