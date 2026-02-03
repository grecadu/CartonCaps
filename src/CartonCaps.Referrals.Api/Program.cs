using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Application.Contracts;
using CartonCaps.Referrals.Application.Services;
using CartonCaps.Referrals.Domain.Referrals;
using CartonCaps.Referrals.Infrastructure.Links;
using CartonCaps.Referrals.Infrastructure.Persistence;
using CartonCaps.Referrals.Infrastructure.Persistence.EF;
using CartonCaps.Referrals.Infrastructure.Time;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();

string persistenceProvider = builder.Configuration["Persistence:Provider"] ?? "InMemory";

if (persistenceProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    string connectionString = builder.Configuration.GetConnectionString("ReferralsDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:ReferralsDb");

    builder.Services.AddDbContext<ReferralsDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<IReferralRepository, EfReferralRepository>();
}
else
{
    builder.Services.AddSingleton<IReferralRepository, InMemoryReferralRepository>();
}

builder.Services.AddSingleton<IReferralLinkService>(serviceProvider =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string baseUrl = configuration["ReferralLinks:BaseUrl"] ?? "http://localhost:5085";
    return new SimpleReferralLinkService(new Uri(baseUrl));
});

builder.Services.AddScoped<ReferralService>();
builder.Services.AddLogging();

const string CorsPolicyName = "CartonCapsCors";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (persistenceProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    using IServiceScope scope = app.Services.CreateScope();
    ReferralsDbContext referralsDbContext = scope.ServiceProvider.GetRequiredService<ReferralsDbContext>();
    referralsDbContext.Database.Migrate();
}

app.UseStaticFiles();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async httpContext =>
    {
        IExceptionHandlerPathFeature? exceptionFeature = httpContext.Features.Get<IExceptionHandlerPathFeature>();
        Exception? exception = exceptionFeature?.Error;

        if (exception is ReferralService.ReferralAppException referralAppException)
        {
            int statusCode = referralAppException.Code switch
            {
                "rate_limited" => StatusCodes.Status429TooManyRequests,
                "forbidden" => StatusCodes.Status403Forbidden,
                "not_found" => StatusCodes.Status404NotFound,
                "unauthorized" => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status400BadRequest
            };

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            var problemDetails = new ProblemDetails
            {
                Title = referralAppException.Code,
                Detail = referralAppException.Message,
                Status = statusCode,
                Instance = exceptionFeature?.Path
            };

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return;
        }

        if (exception is ArgumentException argumentException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";

            var problemDetails = new ProblemDetails
            {
                Title = "validation_error",
                Detail = argumentException.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = exceptionFeature?.Path
            };

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return;
        }

        Log.Error(exception, "Unhandled exception");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync("{\"error\":\"Internal Server Error\"}");
    });
});

if (app.Environment.IsDevelopment())
{
    app.Use(async (httpContext, next) =>
    {
        if (!httpContext.Request.Headers.ContainsKey("X-User-Id"))
            httpContext.Request.Headers["X-User-Id"] = "11111111-1111-1111-1111-111111111111";

        if (!httpContext.Request.Headers.ContainsKey("X-Referral-Code"))
            httpContext.Request.Headers["X-Referral-Code"] = "ABC12345";

        await next();
    });

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicyName);


app.MapGet("/", () => Results.Ok(new { service = "CartonCaps.Referrals.Mock", version = "1.0" }));

app.MapGet("/debug/whoami", (HttpContext httpContext) =>
{
    string? userIdHeader = httpContext.Request.Headers.TryGetValue("X-User-Id", out var rawUserIdHeader)
        ? rawUserIdHeader.ToString()
        : null;

    string? referralCodeHeader = httpContext.Request.Headers.TryGetValue("X-Referral-Code", out var rawReferralCodeHeader)
        ? rawReferralCodeHeader.ToString()
        : null;

    return Results.Ok(new
    {
        headers = new
        {
            XUserId = userIdHeader,
            XReferralCode = referralCodeHeader
        }
    });
});


app.MapGet("/appstore", () => Results.Redirect("/appstore.html", false));


app.MapGet("/r/{token}", (HttpContext ctx, string token) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var redirectUrl = $"{baseUrl}/appstore.html?token={Uri.EscapeDataString(token)}";
    return Results.Redirect(redirectUrl, false);
});



var referralsGroup = app.MapGroup("/v1/referrals");

referralsGroup.MapPost("", async (ReferralService service, ICurrentUserContext currentUserContext, CreateReferralRequest request, CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(currentUserContext.UserId, currentUserContext.ReferralCode, request, cancellationToken);
    return Results.Created($"/v1/referrals/{result.ReferralId}", result);
});

referralsGroup.MapGet("", async (ReferralService service, ICurrentUserContext currentUserContext, ReferralStatus? status, int skip, int take, CancellationToken cancellationToken) =>
{
    var result = await service.ListAsync(currentUserContext.UserId, status, skip, take, cancellationToken);
    return Results.Ok(result);
});

referralsGroup.MapGet("{id:guid}", async (ReferralService service, ICurrentUserContext currentUserContext, Guid id, CancellationToken cancellationToken) =>
{
    var item = await service.GetAsync(currentUserContext.UserId, id, cancellationToken);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});

referralsGroup.MapGet("resolve", async (ReferralService service, string token, CancellationToken cancellationToken) =>
{
    var result = await service.ResolveAsync(token, cancellationToken);
    return Results.Ok(result);
});

referralsGroup.MapPost("{id:guid}/events", async (ReferralService service, ICurrentUserContext currentUserContext, Guid id, TrackEventRequest request, CancellationToken cancellationToken) =>
{
    await service.TrackEventAsync(currentUserContext.UserId, id, request.EventType, cancellationToken);
    return Results.NoContent();
});

app.Run();

public sealed record TrackEventRequest(ReferralEventType EventType);

public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentUserContext(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException();

            if (!httpContext.Request.Headers.TryGetValue("X-User-Id", out var rawUserIdHeader) ||
                !Guid.TryParse(rawUserIdHeader, out Guid userId))
            {
                throw new ReferralService.ReferralAppException("unauthorized", "Missing or invalid X-User-Id header.");
            }

            return userId;
        }
    }

    public string ReferralCode
    {
        get
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException();

            if (!httpContext.Request.Headers.TryGetValue("X-Referral-Code", out var rawReferralCodeHeader) ||
                string.IsNullOrWhiteSpace(rawReferralCodeHeader))
            {
                throw new ReferralService.ReferralAppException("unauthorized", "Missing X-Referral-Code header.");
            }

            return rawReferralCodeHeader.ToString();
        }
    }
}
