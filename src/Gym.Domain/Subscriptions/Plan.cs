namespace Gym.Domain;

/// <summary>A membership plan: number of months and its price. Prices are edited by the admin.</summary>
public class Plan
{
    public int Id { get; set; }

    public int Months { get; set; }

    /// <summary>US dollars.</summary>
    public decimal Price { get; set; }

    /// <summary>Inactive plans are hidden from reception but kept for old subscriptions.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The default plans with the sample prices from the plan; the admin edits them.</summary>
    public static IReadOnlyList<Plan> Defaults() =>
    [
        new() { Months = 1, Price = 20m },
        new() { Months = 3, Price = 55m },
        new() { Months = 6, Price = 100m },
        new() { Months = 12, Price = 180m },
    ];
}
