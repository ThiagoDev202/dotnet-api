using System.Text;
using System.Threading.RateLimiting;
using Npgsql;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderService.Api.Middleware;
using OrderService.Application.DTOs;
using OrderService.Application.Security;
using OrderService.Application.Services;
using OrderService.Application.Validators;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Extensions;
using OrderService.Infrastructure.Persistence;

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

builder.Services.AddScoped<IRefreshTokenService>(sp =>
    new RefreshTokenService(
        sp.GetRequiredService<IRefreshTokenRepository>(),
        sp.GetRequiredService<IJwtTokenService>(),
        sp.GetRequiredService<IUnitOfWork>(),
        refreshDays));

builder.Services.AddScoped<ILogoutService, LogoutService>();

// ── Validators (FluentValidation) ────────────────────────────────────────────
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.AddScoped<IValidator<TokenRequest>, TokenRequestValidator>();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("JWT Key não configurada. Defina 'Jwt:Key' nas variáveis de ambiente.");

if (jwtKey.Length < 32)
    throw new InvalidOperationException(
        $"JWT Key insuficiente ({jwtKey.Length} chars). " +
        "HMAC-SHA256 requer mínimo 32 caracteres. " +
        "Gere uma chave segura com: openssl rand -base64 32");

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

// ── ForwardedHeaders (para rate limiting correto atrás de proxy) ─────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    // Em produção, restringir aos IPs conhecidos dos proxies:
    // options.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));
});

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

// ── CORS (necessário para cookies cross-origin com AllowCredentials) ──────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowCredentials()
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE");
    });
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
                      "Obtenha um token JWT em POST /auth/token e use no botão 'Authorize'. " +
                      "⚠️ POST /auth/token é um mock de autenticação para demonstração — " +
                      "em produção, use um IdP real (Keycloak, Auth0, Azure AD B2C)."
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

// Valida JWT Key padrão apenas em produção (após Build, configuração efetiva já aplicada)
var effectiveJwtKey = app.Configuration["Jwt:Key"] ?? "";
if (effectiveJwtKey.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
    && app.Environment.IsProduction())
    throw new InvalidOperationException(
        "JWT Key ainda está com valor padrão. Configure uma chave aleatória e segura via variável de ambiente Jwt__Key.");

// ── Migrations automáticas no startup ────────────────────────────────────────
// As migrations executam DDL privilegiado (CREATE ROLE, ENABLE RLS, GRANT), então
// rodam com a conexão de migração (superuser). O runtime conecta com uma role
// restrita (orders_app) sujeita ao RLS — ver ConnectionStrings no compose.
// Em ambientes sem MigrationConnection definida (ex.: testes), cai na DefaultConnection.
{
    var migrationConnection =
        app.Configuration.GetConnectionString("MigrationConnection")
        ?? app.Configuration.GetConnectionString("DefaultConnection");

    var migrationOptions = new DbContextOptionsBuilder<OrderServiceDbContext>()
        .UseNpgsql(migrationConnection,
            npgsql => npgsql.MigrationsAssembly(typeof(OrderServiceDbContext).Assembly.FullName))
        .UseSnakeCaseNamingConvention()
        .Options;

    await using var migrationDb = new OrderServiceDbContext(migrationOptions);
    await migrationDb.Database.MigrateAsync();

    // Sincroniza a senha da role de runtime a cada startup usando a conexão de superuser.
    // Migrations rodam uma vez, mas a senha pode mudar via .env (ex.: rotação). Executar aqui
    // garante consistência independente do estado do volume.
    // Lê a senha da DefaultConnection (já injetada pelo compose) — APP_DB_PASSWORD não é
    // exposta como variável de ambiente no container, apenas interpolada no compose.
    var runtimeConnection = app.Configuration.GetConnectionString("DefaultConnection") ?? "";
    var appDbPassword = new Npgsql.NpgsqlConnectionStringBuilder(runtimeConnection).Password;
    if (!string.IsNullOrWhiteSpace(appDbPassword) && !string.IsNullOrWhiteSpace(migrationConnection))
    {
        await using var syncConn = new Npgsql.NpgsqlConnection(migrationConnection);
        await syncConn.OpenAsync();

        // set_config primeiro (usa parâmetro SQL — sem interpolação)
        await using var setCmd = syncConn.CreateCommand();
        setCmd.CommandText = "SELECT set_config('app.orders_app_password', $1, false)";
        setCmd.Parameters.AddWithValue(appDbPassword);
        await setCmd.ExecuteNonQueryAsync();

        // DDL (ALTER ROLE) não aceita parâmetros posicionais ($1/$2).
        // format('%L', valor) é o mecanismo equivalente do PostgreSQL para DDL: escapa aspas simples,
        // prevenindo SQL injection — análogo a um prepared statement em contexto de DDL.
        await using var alterCmd = syncConn.CreateCommand();
        alterCmd.CommandText = """
            DO $$
            DECLARE v_pass text := current_setting('app.orders_app_password', true);
            BEGIN
                IF v_pass IS NOT NULL AND v_pass <> '' THEN
                    EXECUTE format('ALTER ROLE orders_app PASSWORD %L', v_pass);
                END IF;
            END
            $$;
            """;
        await alterCmd.ExecuteNonQueryAsync();
    }
}

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
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'");
    await next();
});

app.UseHttpsRedirection();

// ForwardedHeaders deve estar ANTES de UseRateLimiter para que RemoteIpAddress
// reflita o IP real do cliente quando a API estiver atrás de proxy reverso.
app.UseForwardedHeaders();
app.UseRateLimiter();

// Swagger controlado por variável — exposto em Development ou quando SwaggerEnabled=true
var swaggerEnabled = app.Environment.IsDevelopment() ||
                     builder.Configuration.GetValue<bool>("SwaggerEnabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1");
        options.DisplayRequestDuration();
    });
}

// CORS antes de Authentication para que o preflight OPTIONS seja tratado corretamente
app.UseCors("AllowFrontend");

app.UseAuthentication();

// Verifica blacklist de JTIs após autenticação — rejeita tokens revogados por logout
app.UseMiddleware<TokenBlacklistMiddleware>();

app.UseAuthorization();

app.MapControllers()
   .RequireRateLimiting("global");

app.MapHealthChecks("/health");

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program { }
