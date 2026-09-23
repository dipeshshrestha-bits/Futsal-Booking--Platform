using Backend.DTOs.Admin;

namespace Backend.DTOs.Owner;

public class OwnerDashboardDto
{
    public int FutsalId { get; set; }
    public string FutsalName { get; set; } = string.Empty;
    public bool IsProfileComplete { get; set; }
    public bool IsListedPublicly { get; set; }
    public int CourtCount { get; set; }

    public int TodayBookingsCount { get; set; }
    public int WeekBookings { get; set; }
    public int MonthBookings { get; set; }

    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }

    public double BookingsGrowth { get; set; }
    public double RevenueGrowth { get; set; }

    /// <summary>Booked share of today's available slots, as a percentage.</summary>
    public double OccupancyRate { get; set; }

    public decimal PendingCashAmount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int UnansweredReviews { get; set; }

    public List<GrowthPointDto> Growth { get; set; } = new();
    public List<OwnerBookingDto> TodaysBookings { get; set; } = new();
    public List<OwnerBookingDto> PendingCashPayments { get; set; } = new();
}