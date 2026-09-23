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

/* ---------------- Settings ---------------- */
builder.Services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));
builder.Services.Configure<ReviewOptions>(configuration.GetSection(ReviewOptions.SectionName));
builder.Services.Configure<EsewaOptions>(configuration.GetSection(EsewaOptions.SectionName));
builder.Services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be set and at least 32 characters long (see appsettings).");
}

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing (see appsettings.Development.json).");
}

/* ---------------- Database ---------------- */
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

/* ---------------- Core services ---------------- */
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddHttpClient("esewa", client => client.Timeout = TimeSpan.FromSeconds(20));

builder.Services.AddScoped<Backend.Services.IAuthService, Backend.Services.AuthService>();
builder.Services.AddScoped<Backend.Services.IAdminService, Backend.Services.AdminService>();
builder.Services.AddScoped<Backend.Services.ISlotAvailabilityService, Backend.Services.SlotAvailabilityService>();
builder.Services.AddScoped<Backend.Services.IOwnerService, Backend.Services.OwnerService>();
builder.Services.AddScoped<Backend.Services.IFutsalService, Backend.Services.FutsalService>();
builder.Services.AddScoped<Backend.Services.IBookingService, Backend.Services.BookingService>();
builder.Services.AddScoped<Backend.Services.IEsewaPaymentService, Backend.Services.EsewaPaymentService>();

/* ---------------- Controllers + JSON ---------------- */
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    });

// Validation errors use the same { success, data, error } envelope
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(ApiResponse.FromModelState(context.ModelState));
});

/* ---------------- Authentication (JWT) ---------------- */
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "username",
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail("unauthorized", "Please sign in to continue."));
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail("forbidden", "You do not have permission to do that."));
            },
        };
    });

builder.Services.AddAuthorization();

/* ---------------- CORS ---------------- */
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

/* ---------------- Swagger ---------------- */
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Futsal Booking API",
        Version = "v1",
        Description = "Admin, Owner and public Player endpoints. Log in, copy the token, then click Authorize.",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/admin/login or /api/auth/owner/login.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });

    options.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Example = new OpenApiString("18:00") });
});

/* ================= Pipeline ================= */
var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || configuration.GetValue<bool>("Swagger:EnableInProduction"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Futsal Booking API");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health", async (ApplicationDbContext db) =>
        Results.Ok(ApiResponse<object>.Ok(new
        {
            status = "ok",
            database = await db.Database.CanConnectAsync(),
            time = DateTime.UtcNow,
        })))
    .WithTags("Health");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();