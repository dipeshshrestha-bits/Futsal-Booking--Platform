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
// JWT CONFIGURATION
// ============================================================

var jwt = configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.Secret))
{
    throw new InvalidOperationException(
        "JWT Secret is missing. Configure Jwt:Secret.");
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
// DATABASE CONNECTION
// ============================================================

var connectionString =
    configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing.");
}


// ============================================================
// DATABASE - POSTGRESQL
// ============================================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});


// ============================================================
// CORE SERVICES
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
// CONTROLLERS + JSON
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
            ApiResponse.FromModelState(context.ModelState));
});


// ============================================================
// JWT AUTHENTICATION
// ============================================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                        Encoding.UTF8.GetBytes(jwt.Secret)),

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromMinutes(1),

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
// (Not needed when the frontend is served from the same site,
//  kept so local development with a separate frontend still works)
// ============================================================

var allowedOrigins =
    configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[]
    {
        "https://dipeshstha-001-site1.etempurl.com",
        "http://localhost:5173",
        "http://localhost:3000"
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
                "Admin, Owner and public Player endpoints."
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Paste your JWT token here."
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
            Example = new OpenApiString("18:00")
        });
});


// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();


// ============================================================
// DATABASE SEEDING
// ============================================================

var seedOnStartup =
    configuration.GetValue<bool?>(
        "Database:SeedOnStartup") ?? false;

if (seedOnStartup)
{
    try
    {
        await DbSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE SEEDING FAILED:");

        Console.WriteLine(ex);

        throw;
    }
}


// ============================================================
// EXCEPTION HANDLING
// ============================================================

app.UseMiddleware<ExceptionHandlingMiddleware>();


// ============================================================
// SWAGGER
// ============================================================

var swaggerEnabled =
    app.Environment.IsDevelopment()
    || configuration.GetValue<bool>(
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
// STATIC FILES (frontend build lives in wwwroot)
// UseDefaultFiles MUST come before UseStaticFiles
// ============================================================

app.UseDefaultFiles();

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
// AUTHENTICATION / AUTHORIZATION
// ============================================================

app.UseAuthentication();

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
        try
        {
            await db.Database.OpenConnectionAsync();

            return Results.Ok(
                new
                {
                    status = "ok",
                    database = true,
                    environment =
                        app.Environment.EnvironmentName,
                    time = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            return Results.Ok(
                new
                {
                    status = "error",
                    database = false,
                    environment =
                        app.Environment.EnvironmentName,
                    error = ex.Message,
                    innerError =
                        ex.InnerException?.Message
                });
        }
        finally
        {
            try
            {
                await db.Database.CloseConnectionAsync();
            }
            catch
            {
            }
        }
    })
    .WithTags("Health");


// ============================================================
// SPA FALLBACK - MUST BE LAST
// Any path that is not /api/... or /swagger... returns
// index.html so frontend routes (/login, /bookings) work
// on page refresh.
// The old "/" test endpoint was removed because it blocked
// the frontend from loading.
// ============================================================

app.MapFallbackToFile(
    "{*path:regex(^(?!api/|swagger).*$)}",
    "index.html");


// ============================================================
// RUN
// ============================================================

app.Run();