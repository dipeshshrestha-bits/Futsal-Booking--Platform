using System.Text;
using System.Text.Json.Serialization;

using Backend.Common;
using Backend.Data;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;


// ============================================================
// SETTINGS
// ============================================================

builder.Services.Configure<JwtOptions>(
    configuration.GetSection(JwtOptions.SectionName));

builder.Services.Configure<AppOptions>(
    configuration.GetSection(AppOptions.SectionName));

builder.Services.Configure<BookingOptions>(
    configuration.GetSection(BookingOptions.SectionName));

builder.Services.Configure<ReviewOptions>(
    configuration.GetSection(ReviewOptions.SectionName));

builder.Services.Configure<EsewaOptions>(
    configuration.GetSection(EsewaOptions.SectionName));

builder.Services.Configure<SeedAdminOptions>(
    configuration.GetSection(SeedAdminOptions.SectionName));

builder.Services.Configure<DatabaseOptions>(
    configuration.GetSection(DatabaseOptions.SectionName));


// ============================================================
// JWT
// ============================================================

var jwt = configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>();

if (jwt == null)
{
    throw new InvalidOperationException(
        "JWT configuration is missing.");
}

if (string.IsNullOrWhiteSpace(jwt.Secret))
{
    throw new InvalidOperationException(
        "Jwt:Secret is missing.");
}

if (jwt.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must contain at least 32 characters.");
}

if (string.IsNullOrWhiteSpace(jwt.Issuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer is missing.");
}

if (string.IsNullOrWhiteSpace(jwt.Audience))
{
    throw new InvalidOperationException(
        "Jwt:Audience is missing.");
}


// ============================================================
// DATABASE
// ============================================================

var connectionString =
    configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});


// ============================================================
// SERVICES
// ============================================================

builder.Services.AddSingleton<IAppClock, AppClock>();

builder.Services.AddHttpClient(
    "esewa",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(20);
    });

builder.Services.AddScoped<
    Backend.Services.IAuthService,
    Backend.Services.AuthService>();

builder.Services.AddScoped<
    Backend.Services.IAdminService,
    Backend.Services.AdminService>();

builder.Services.AddScoped<
    Backend.Services.ISlotAvailabilityService,
    Backend.Services.SlotAvailabilityService>();

builder.Services.AddScoped<
    Backend.Services.IOwnerService,
    Backend.Services.OwnerService>();

builder.Services.AddScoped<
    Backend.Services.IFutsalService,
    Backend.Services.FutsalService>();

builder.Services.AddScoped<
    Backend.Services.IBookingService,
    Backend.Services.BookingService>();

builder.Services.AddScoped<
    Backend.Services.IEsewaPaymentService,
    Backend.Services.EsewaPaymentService>();


// ============================================================
// CONTROLLERS
// ============================================================

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());

        options.JsonSerializerOptions.Converters.Add(
            new TimeOnlyJsonConverter());
    });


// ============================================================
// MODEL VALIDATION
// ============================================================

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(
            ApiResponse.FromModelState(
                context.ModelState));
});


// ============================================================
// AUTHENTICATION
// ============================================================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,

                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwt.Secret)),

                ValidateLifetime = true,

                ClockSkew =
                    TimeSpan.FromMinutes(1),

                RoleClaimType = "role",
                NameClaimType = "username"
            };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                    return;

                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(
                        "unauthorized",
                        "Please sign in to continue."));
            },

            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                    return;

                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(
                        "forbidden",
                        "You do not have permission to do that."));
            }
        };
    });

builder.Services.AddAuthorization();


// ============================================================
// CORS
// ============================================================

var allowedOrigins =
    configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

if (allowedOrigins == null ||
    allowedOrigins.Length == 0)
{
    allowedOrigins = new[]
    {
        "https://dipeshstha-001-site1.etempurl.com"
    };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


// ============================================================
// SWAGGER
// ============================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Futsal Booking API",
            Version = "v1",
            Description =
                "Futsal Booking API"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",

            Type =
                SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT",

            In =
                ParameterLocation.Header,

            Description =
                "Enter JWT token."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,

                            Id = "Bearer"
                        }
                },

                Array.Empty<string>()
            }
        });

    options.MapType<TimeOnly>(
        () => new OpenApiSchema
        {
            Type = "string",
            Example =
                new OpenApiString("18:00")
        });
});


// ============================================================
// BUILD
// ============================================================

var app = builder.Build();


// ============================================================
// DATABASE SEEDING
// ============================================================
//
// IMPORTANT:
// For the FIRST deployment, you can disable seeding
// until the PostgreSQL database is confirmed working.
//
// Set:
// "Database": {
//     "SeedOnStartup": false
// }
// ============================================================

var seedOnStartup =
    configuration.GetValue<bool>(
        "Database:SeedOnStartup");

if (seedOnStartup)
{
    await DbSeeder.SeedAsync(
        app.Services);
}


// ============================================================
// EXCEPTION HANDLING
// ============================================================

app.UseMiddleware<
    ExceptionHandlingMiddleware>();


// ============================================================
// SWAGGER
// ============================================================

var swaggerEnabled =
    app.Environment.IsDevelopment()
    ||
    configuration.GetValue<bool>(
        "Swagger:EnableInProduction");

if (swaggerEnabled)
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle =
            "Futsal Booking API";
    });
}


// ============================================================
// HTTPS
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


// ============================================================
// STATIC FILES
// ============================================================

app.UseStaticFiles();


// ============================================================
// ROUTING
// ============================================================

app.UseRouting();


// ============================================================
// CORS
// ============================================================

app.UseCors("Frontend");


// ============================================================
// AUTHENTICATION
// ============================================================

app.UseAuthentication();


// ============================================================
// AUTHORIZATION
// ============================================================

app.UseAuthorization();


// ============================================================
// CONTROLLERS
// ============================================================

app.MapControllers();


// ============================================================
// HEALTH CHECK
// ============================================================

app.MapGet(
    "/api/health",
    async (ApplicationDbContext db) =>
    {
        var database =
            await db.Database.CanConnectAsync();

        return Results.Ok(
            new
            {
                status = "ok",
                database = database,
                environment =
                    app.Environment.EnvironmentName,
                time = DateTime.UtcNow
            });
    });


// ============================================================
// ROOT
// ============================================================

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                message =
                    "Futsal Booking API is running.",
                status = "ok"
            }));


// ============================================================
// RUN
// ============================================================

app.Run();