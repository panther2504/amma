using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amma.Api.Auth;
using Amma.Api.Services;
using Amma.Infrastructure;
using Amma.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Configuration ----------
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.Configure<OtpOptions>(config.GetSection("Otp"));
builder.Services.Configure<ContentOptions>(config.GetSection("Content"));
builder.Services.Configure<AmmaSignInOptions>(config.GetSection("SignIn"));
var signIn = config.GetSection("SignIn").Get<AmmaSignInOptions>() ?? new AmmaSignInOptions();
if (signIn.IsPin)
{
    if ((config.GetSection("Otp:AllowedDestinations").Get<string[]>() ?? []).Length == 0)
        throw new InvalidOperationException("PIN sign-in is for private use: set Otp:AllowedDestinations to the allowed mobile numbers.");
    if (signIn.SetupCode.Length < 8)
        throw new InvalidOperationException("PIN sign-in needs SignIn:SetupCode (at least 8 characters) so only you can create PINs.");
}
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Set it with user-secrets or the Jwt__SigningKey environment variable.");

// ---------- Infrastructure ----------
builder.Services.AddInfrastructure(config);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();

// ---------- Application services ----------
builder.Services.AddSingleton<UserClock>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ContentQuery>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DoseService>();
builder.Services.AddScoped<IAssistantResponder, RuleBasedResponder>();
builder.Services.AddSingleton<IOtpSender, LoggingOtpSender>();
builder.Services.AddSingleton<IPushSender, NoopPushSender>();
if (config.GetValue("Reminders:Enabled", true)) builder.Services.AddHostedService<ReminderWorker>();

// ---------- Auth ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType = "name",
    };
});
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:AuthPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
});

// ---------- Web ----------
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Amma.Api.Infrastructure.UnauthorizedExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins([.. config.GetSection("Cors:Origins").Get<string[]>() ?? [], "https://localhost", "capacitor://localhost"])
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Idempotent-Replayed", "Content-Disposition")));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers.XFrameOptions = "DENY";
    await next();
});

app.MapOpenApi();
if (app.Environment.IsDevelopment()) app.MapScalarApiReference("/docs");

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Amma.Api.Infrastructure.IdempotencyMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
// Unknown API routes are real 404s; everything else is handled by the single-page app.
app.MapFallback("/api/{**rest}", ctx => { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return Task.CompletedTask; });
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(seedDemoUser: config.GetValue("Seed:DemoUser", false));
}

app.Run();

public partial class Program;
