namespace Backend.DTOs.Admin;

public class GrowthPointDto
{
    /// <summary>"2026-04" — the frontend chart formats this.</summary>
    public string Label { get; set; } = string.Empty;
    public int Bookings { get; set; }
    public decimal Revenue { get; set; }
}

public class ActivityItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TopFutsalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public int BookingCount { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminDashboardDto
{
    public int TotalFutsals { get; set; }
    public int ActiveFutsals { get; set; }
    public int TotalOwners { get; set; }
    public int ActiveOwners { get; set; }

    public int TodayBookings { get; set; }
    public int WeekBookings { get; set; }
    public int MonthBookings { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal TotalRevenue { get; set; }

    public double BookingsGrowth { get; set; }
    public double RevenueGrowth { get; set; }

    public int PendingCashCount { get; set; }
    public decimal PendingCashAmount { get; set; }

    public List<GrowthPointDto> Growth { get; set; } = new();
    public List<ActivityItemDto> RecentActivity { get; set; } = new();
    public List<TopFutsalDto> TopFutsals { get; set; } = new();
}