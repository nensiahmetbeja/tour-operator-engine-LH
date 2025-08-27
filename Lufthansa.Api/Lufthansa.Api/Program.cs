using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Lufthansa.Api.Auth;
using Lufthansa.Api.Auth.Blacklist;
using Lufthansa.Api.Hubs;
using Lufthansa.Api.RealTime;
using Lufthansa.Application.Services;
using Lufthansa.Infrastructure.Persistence;
using Lufthansa.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = new[] { "http://localhost:8080", "http://127.0.0.1:8080" };

builder.Services.AddCors(o => o.AddPolicy("dev", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()  // SignalR needs this
));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Basic doc info (optional)
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Lufthansa API", Version = "v1" });

    // JWT Bearer scheme
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter **ONLY** your JWT. No 'Bearer ' prefix needed.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);

    // Require token by default (so lock icon shows on [Authorize] endpoints)
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// Db
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        opts.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var blacklist = ctx.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (!string.IsNullOrEmpty(jti) && await blacklist.IsBannedAsync(jti))
                {
                    ctx.Fail("Token is revoked");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("TourOperatorOnly", p => p.RequireRole("TourOperator"));
});

// Simple user service (in-memory for now)
builder.Services.AddSingleton<IUserService, InMemoryUserService>();
builder.Services.AddSignalR();

// Token blacklist (memory now; swap to Redis later)
builder.Services.AddSingleton<ITokenBlacklist, InMemoryTokenBlacklist>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITourOperatorService, TourOperatorService>();
builder.Services.AddScoped<IPricingUploadService, PricingUploadService>();
builder.Services.AddScoped<IPricingQueryService, PricingQueryService>();
builder.Services.AddScoped<IProgressNotifier, SignalRProgressNotifier>();

// Redis cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Connection"];
    options.InstanceName = "lh:";
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default"))
    .AddRedis(builder.Configuration["Redis:Connection"]);


var app = builder.Build();
app.UseCors("dev"); 
app.UseStaticFiles();          // serves wwwroot/*

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHub<UploadHub>("/hubs/upload");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();