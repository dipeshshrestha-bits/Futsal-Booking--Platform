using Backend.Common;
using Backend.Data;
using Backend.DTOs.Admin;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class AdminService : IAdminService
{
    private const int BcryptWorkFactor = 12;

    private readonly ApplicationDbContext _db;
    private readonly IAppClock _clock;
    private readonly AppOptions _app;

    public AdminService(ApplicationDbContext db, IAppClock clock, IOptions<AppOptions> app)
    {
        _db = db;
        _clock = clock;
        _app = app.Value;
    }

    /* ==================== Dashboard ==================== */

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var seriesStart = monthStart.AddMonths(-5);

        var futsals = await _db.Futsals
            .Select(f => new { f.IsActive, f.IsProfileComplete, OwnerActive = f.Owner.IsActive })
            .ToListAsync(ct);

        var owners = await _db.Users
            .Where(u => u.Role == UserRole.Owner)
            .Select(u => u.IsActive)
            .ToListAsync(ct);

        // Small dataset: pull the last 6 months and aggregate in memory
        var recent = await _db.Bookings
            .Where(b => b.BookingDate >= seriesStart)
            .Select(b => new
            {
                b.Id,
                b.BookingDate,
                b.TotalAmount,
                b.BookingStatus,
                b.PaymentStatus,
                b.PaymentMethod,
                FutsalId = b.Court.FutsalId,
                FutsalName = b.Court.Futsal.Name,
                City = b.Court.Futsal.City,
            })
            .ToListAsync(ct);

        var live = recent.Where(b => b.BookingStatus != BookingStatus.Cancelled).ToList();
        var thisMonth = live.Where(b => b.BookingDate >= monthStart).ToList();
        var lastMonth = live.Where(b => b.BookingDate >= previousMonthStart && b.BookingDate < monthStart).ToList();

        var growth = Enumerable.Range(0, 6)
            .Select(offset => seriesStart.AddMonths(offset))
            .Select(month =>
            {
                var next = month.AddMonths(1);
                var slice = live.Where(b => b.BookingDate >= month && b.BookingDate < next).ToList();
                return new GrowthPointDto
                {
                    Label = $"{month.Year:D4}-{month.Month:D2}",
                    Bookings = slice.Count,
                    Revenue = slice.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount),
                };
            })
            .ToList();

        var pendingCash = live
            .Where(b => b.PaymentMethod == PaymentMethod.Cash && b.PaymentStatus == PaymentStatus.Pending)
            .ToList();

        var topFutsals = thisMonth
            .GroupBy(b => new { b.FutsalId, b.FutsalName, b.City })
            .Select(g => new TopFutsalDto
            {
                Id = g.Key.FutsalId,
                Name = g.Key.FutsalName,
                City = g.Key.City,
                BookingCount = g.Count(),
                Revenue = g.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount),
            })
            .OrderByDescending(f => f.Revenue)
            .ThenByDescending(f => f.BookingCount)
            .Take(5)
            .ToList();

        var totalRevenue = await _db.Bookings
            .Where(b => b.PaymentStatus == PaymentStatus.Paid && b.BookingStatus != BookingStatus.Cancelled)
            .SumAsync(b => (decimal?)b.TotalAmount, ct) ?? 0m;

        return new AdminDashboardDto
        {
            TotalFutsals = futsals.Count,
            ActiveFutsals = futsals.Count(f => f.IsActive && f.IsProfileComplete && f.OwnerActive),
            TotalOwners = owners.Count,
            ActiveOwners = owners.Count(active => active),

            TodayBookings = live.Count(b => b.BookingDate == today),
            WeekBookings = live.Count(b => b.BookingDate >= weekStart),
            MonthBookings = thisMonth.Count,
            MonthRevenue = thisMonth.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount),
            TotalRevenue = totalRevenue,

            BookingsGrowth = PercentChange(thisMonth.Count, lastMonth.Count),
            RevenueGrowth = PercentChange(
                thisMonth.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount),
                lastMonth.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount)),

            PendingCashCount = pendingCash.Count,
            PendingCashAmount = pendingCash.Sum(b => b.TotalAmount),

            Growth = growth,
            TopFutsals = topFutsals,
            RecentActivity = await GetRecentActivityAsync(ct),
        };
    }

    private async Task<List<ActivityItemDto>> GetRecentActivityAsync(CancellationToken ct)
    {
        var bookings = await _db.Bookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new
            {
                b.Id,
                b.ReferenceCode,
                b.PlayerName,
                b.TotalAmount,
                b.BookingStatus,
                b.PaymentMethod,
                b.CreatedAt,
                b.CancelledAt,
                FutsalName = b.Court.Futsal.Name,
            })
            .ToListAsync(ct);

        var items = bookings.Select(b => b.BookingStatus == BookingStatus.Cancelled
            ? new ActivityItemDto
            {
                Id = $"booking-{b.Id}-cancelled",
                Type = "booking_cancelled",
                Title = $"Booking {b.ReferenceCode} cancelled",
                Detail = b.FutsalName,
                Amount = b.TotalAmount,
                CreatedAt = b.CancelledAt ?? b.CreatedAt,
            }
            : new ActivityItemDto
            {
                Id = $"booking-{b.Id}",
                Type = "booking_created",
                Title = $"New {DisplayText.PaymentMethodLabel(b.PaymentMethod)} booking by {b.PlayerName}",
                Detail = b.FutsalName,
                Amount = b.TotalAmount,
                CreatedAt = b.CreatedAt,
            }).ToList();

        var newOwners = await _db.Users
            .Where(u => u.Role == UserRole.Owner)
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new ActivityItemDto
            {
                Id = $"owner-{u.Id}",
                Type = "owner_created",
                Title = $"Owner account created for {u.FullName}",
                Detail = u.Futsal != null ? u.Futsal.Name : null,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);

        return items.Concat(newOwners)
            .OrderByDescending(item => item.CreatedAt)
            .Take(12)
            .ToList();
    }

    private static double PercentChange(decimal current, decimal previous)
    {
        if (previous <= 0) return current > 0 ? 100d : 0d;
        return Math.Round((double)((current - previous) / previous) * 100d, 1);
    }

    /* ==================== Owners ==================== */

    public async Task<List<OwnerListItemDto>> GetOwnersAsync(string? search, CancellationToken ct = default)
    {
        var query = _db.Users.Where(u => u.Role == UserRole.Owner);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, term) ||
                EF.Functions.ILike(u.Email, term) ||
                EF.Functions.ILike(u.Username, term) ||
                (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, term)) ||
                (u.Futsal != null && EF.Functions.ILike(u.Futsal.Name, term)));
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new OwnerListItemDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                MustChangePassword = u.MustChangePassword,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                FutsalId = u.Futsal != null ? u.Futsal.Id : null,
                FutsalName = u.Futsal != null ? u.Futsal.Name : null,
                City = u.Futsal != null ? u.Futsal.City : null,
                IsProfileComplete = u.Futsal != null && u.Futsal.IsProfileComplete,
                FutsalIsActive = u.Futsal != null && u.Futsal.IsActive,
                CourtCount = u.Futsal != null ? u.Futsal.Courts.Count : 0,
                BookingCount = u.Futsal != null
                    ? u.Futsal.Courts.SelectMany(c => c.Bookings).Count(b => b.BookingStatus != BookingStatus.Cancelled)
                    : 0,
            })
            .ToListAsync(ct);
    }

    public async Task<OwnerCredentialsDto> CreateOwnerAsync(CreateOwnerDto request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (!InputRules.IsValidEmail(email))
        {
            throw AppException.Validation("email", "Enter a valid email address.");
        }

        var phone = InputRules.NormalizePhone(request.PhoneNumber);
        if (!InputRules.IsValidNepalPhone(phone))
        {
            throw AppException.Validation("phoneNumber", "Enter a valid 10-digit mobile number.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw AppException.Conflict("An account with this email already exists.", "email_taken");
        }

        var username = await BuildUniqueUsernameAsync(request.Username, email, ct);
        var temporaryPassword = CodeGenerator.TemporaryPassword();

        var owner = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Username = username,
            PhoneNumber = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword, BcryptWorkFactor),
            Role = UserRole.Owner,
            IsActive = true,
            MustChangePassword = true,
        };

        var futsal = new Futsal
        {
            Owner = owner,
            Name = request.FutsalName.Trim(),
            City = InputRules.Clean(request.City),
            Address = InputRules.Clean(request.Address),
            ContactNumber = phone,
            IsActive = true,
        };
        futsal.IsProfileComplete = IsProfileComplete(futsal);

        _db.Users.Add(owner);
        _db.Futsals.Add(futsal);
        await _db.SaveChangesAsync(ct);

        return new OwnerCredentialsDto
        {
            OwnerId = owner.Id,
            FullName = owner.FullName,
            Username = owner.Username,
            Email = owner.Email,
            TemporaryPassword = temporaryPassword,
            LoginUrl = $"{_app.FrontendBaseUrl.TrimEnd('/')}/owner/login",
            FutsalId = futsal.Id,
            FutsalName = futsal.Name,
        };
    }

    public async Task<OwnerListItemDto> SetOwnerStatusAsync(int ownerId, bool isActive, CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync(ownerId, ct);

        owner.IsActive = isActive;
        owner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (await GetOwnersAsync(null, ct)).First(o => o.Id == ownerId);
    }

    public async Task<OwnerCredentialsDto> ResetOwnerPasswordAsync(int ownerId, CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync(ownerId, ct);
        var temporaryPassword = CodeGenerator.TemporaryPassword();

        owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword, BcryptWorkFactor);
        owner.MustChangePassword = true;
        owner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new OwnerCredentialsDto
        {
            OwnerId = owner.Id,
            FullName = owner.FullName,
            Username = owner.Username,
            Email = owner.Email,
            TemporaryPassword = temporaryPassword,
            LoginUrl = $"{_app.FrontendBaseUrl.TrimEnd('/')}/owner/login",
            FutsalId = owner.Futsal?.Id,
            FutsalName = owner.Futsal?.Name,
        };
    }

    private async Task<User> GetOwnerAsync(int ownerId, CancellationToken ct) =>
        await _db.Users
            .Include(u => u.Futsal)
            .FirstOrDefaultAsync(u => u.Id == ownerId && u.Role == UserRole.Owner, ct)
        ?? throw AppException.NotFound("Owner not found.");

    private async Task<string> BuildUniqueUsernameAsync(string? requested, string email, CancellationToken ct)
    {
        var baseName = InputRules.Clean(requested)?.ToLowerInvariant() ?? email.Split('@')[0].ToLowerInvariant();
        baseName = new string(baseName.Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-').ToArray());
        if (baseName.Length < 3) baseName = $"owner{baseName}";
        if (baseName.Length > 40) baseName = baseName[..40];

        var candidate = baseName;
        var suffix = 1;
        while (await _db.Users.AnyAsync(u => u.Username == candidate, ct))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }
        return candidate;
    }

    private static bool IsProfileComplete(Futsal futsal) =>
        !string.IsNullOrWhiteSpace(futsal.Name) &&
        !string.IsNullOrWhiteSpace(futsal.Address) &&
        !string.IsNullOrWhiteSpace(futsal.City) &&
        !string.IsNullOrWhiteSpace(futsal.ContactNumber);

    /* ==================== Futsals ==================== */

    public async Task<List<AdminFutsalListItemDto>> GetFutsalsAsync(CancellationToken ct = default) =>
        await _db.Futsals
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new AdminFutsalListItemDto
            {
                Id = f.Id,
                Name = f.Name,
                City = f.City,
                Address = f.Address,
                ContactNumber = f.ContactNumber,
                IsActive = f.IsActive,
                IsProfileComplete = f.IsProfileComplete,
                IsListedPublicly = f.IsActive && f.IsProfileComplete && f.Owner.IsActive && f.Courts.Any(c => c.IsActive),
                CreatedAt = f.CreatedAt,
                OwnerId = f.OwnerId,
                OwnerName = f.Owner.FullName,
                OwnerEmail = f.Owner.Email,
                OwnerIsActive = f.Owner.IsActive,
                CourtCount = f.Courts.Count,
                BookingCount = f.Courts.SelectMany(c => c.Bookings).Count(b => b.BookingStatus != BookingStatus.Cancelled),
                AverageRating = f.Reviews.Any() ? Math.Round(f.Reviews.Average(r => (double)r.Rating), 2) : 0,
                ReviewCount = f.Reviews.Count,
            })
            .ToListAsync(ct);

    public async Task<AdminFutsalListItemDto> SetFutsalStatusAsync(int futsalId, bool isActive, CancellationToken ct = default)
    {
        var futsal = await _db.Futsals.FirstOrDefaultAsync(f => f.Id == futsalId, ct)
            ?? throw AppException.NotFound("Futsal not found.");

        futsal.IsActive = isActive;
        futsal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (await GetFutsalsAsync(ct)).First(f => f.Id == futsalId);
    }

    /* ==================== Bookings ==================== */

    public async Task<List<AdminBookingListItemDto>> GetBookingsAsync(AdminBookingFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.Bookings.AsQueryable();

        if (filter.From.HasValue) query = query.Where(b => b.BookingDate >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(b => b.BookingDate <= filter.To.Value);
        if (filter.FutsalId.HasValue) query = query.Where(b => b.Court.FutsalId == filter.FutsalId.Value);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(b => b.Court.Futsal.City != null && EF.Functions.ILike(b.Court.Futsal.City, city));
        }

        if (DisplayText.ParseEnumFilter<BookingStatus>(filter.Status) is { } status)
        {
            query = query.Where(b => b.BookingStatus == status);
        }

        if (DisplayText.ParseEnumFilter<PaymentStatus>(filter.PaymentStatus) is { } paymentStatus)
        {
            query = query.Where(b => b.PaymentStatus == paymentStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
        {
            var method = DisplayText.ParsePaymentMethod(filter.PaymentMethod);
            query = query.Where(b => b.PaymentMethod == method);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(b =>
                EF.Functions.ILike(b.ReferenceCode, term) ||
                EF.Functions.ILike(b.PlayerName, term) ||
                EF.Functions.ILike(b.PlayerContact, term) ||
                EF.Functions.ILike(b.Court.Futsal.Name, term));
        }

        var rows = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .Take(500)
            .Select(b => new
            {
                b.Id,
                b.ReferenceCode,
                FutsalId = b.Court.FutsalId,
                FutsalName = b.Court.Futsal.Name,
                FutsalAddress = b.Court.Futsal.Address,
                City = b.Court.Futsal.City,
                b.CourtId,
                CourtName = b.Court.Name,
                b.BookingDate,
                b.StartTime,
                b.EndTime,
                b.PlayerName,
                b.PlayerContact,
                b.PlayerEmail,
                b.TotalAmount,
                b.PaymentMethod,
                b.PaymentStatus,
                b.BookingStatus,
                b.Source,
                b.CreatedAt,
            })
            .ToListAsync(ct);

        return rows.Select(b => new AdminBookingListItemDto
        {
            Id = b.Id,
            ReferenceCode = b.ReferenceCode,
            FutsalId = b.FutsalId,
            FutsalName = b.FutsalName,
            FutsalAddress = b.FutsalAddress,
            City = b.City,
            CourtId = b.CourtId,
            CourtName = b.CourtName,
            Date = b.BookingDate,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            PlayerName = b.PlayerName,
            ContactNumber = b.PlayerContact,
            Email = b.PlayerEmail,
            Amount = b.TotalAmount,
            PaymentMethod = DisplayText.PaymentMethodLabel(b.PaymentMethod),
            PaymentStatus = b.PaymentStatus.ToString(),
            Status = b.BookingStatus.ToString(),
            Source = b.Source.ToString(),
            CreatedAt = b.CreatedAt,
        }).ToList();
    }

    /* ==================== Transactions ==================== */

    public async Task<List<AdminTransactionDto>> GetTransactionsAsync(AdminTransactionFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.Payments.AsQueryable();

        if (filter.From.HasValue)
        {
            var fromUtc = _clock.ToUtc(filter.From.Value, TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt >= fromUtc);
        }

        if (filter.To.HasValue)
        {
            var toUtc = _clock.ToUtc(filter.To.Value.AddDays(1), TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt < toUtc);
        }

        if (filter.FutsalId.HasValue)
        {
            query = query.Where(p => p.Booking.Court.FutsalId == filter.FutsalId.Value);
        }

        if (DisplayText.ParseEnumFilter<TransactionMethod>(filter.Method) is { } method)
        {
            query = query.Where(p => p.Method == method);
        }

        if (DisplayText.ParseEnumFilter<TransactionStatus>(filter.Status) is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(500)
            .Select(p => new
            {
                p.Id,
                p.BookingId,
                BookingReference = p.Booking.ReferenceCode,
                PlayerName = p.Booking.PlayerName,
                FutsalName = p.Booking.Court.Futsal.Name,
                City = p.Booking.Court.Futsal.City,
                p.Amount,
                p.Method,
                p.Status,
                p.GatewayTransactionUuid,
                p.GatewayRefId,
                p.CreatedAt,
            })
            .ToListAsync(ct);

        return rows.Select(p => new AdminTransactionDto
        {
            Id = p.Id,
            BookingId = p.BookingId,
            BookingReference = p.BookingReference,
            PlayerName = p.PlayerName,
            FutsalName = p.FutsalName,
            City = p.City,
            Amount = p.Amount,
            PaymentMethod = p.Method.ToString(),
            Status = p.Status.ToString(),
            TransactionCode = p.GatewayRefId ?? p.GatewayTransactionUuid ?? p.BookingReference,
            GatewayRefId = p.GatewayRefId,
            CreatedAt = p.CreatedAt,
        }).ToList();
    }
}