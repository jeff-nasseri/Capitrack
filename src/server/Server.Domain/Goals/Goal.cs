namespace Server.Domain.Goals;

/// <summary>A savings/wealth target with a due date. Aggregate root.</summary>
public sealed class Goal : AggregateRoot<int>
{
    /// <summary>The goal's title.</summary>
    public string Title { get; private set; } = "";

    /// <summary>The target amount to reach.</summary>
    public double TargetAmount { get; private set; }

    /// <summary>The date by which the goal should be met.</summary>
    public TradeDate TargetDate { get; private set; } = default!;

    /// <summary>A free-text description.</summary>
    public string Description { get; private set; } = "";

    /// <summary>Whether the goal has been achieved.</summary>
    public bool Achieved { get; private set; }

    /// <summary>The optional owning category's identifier.</summary>
    public int? CategoryId { get; private set; }

    /// <summary>When the goal was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>When the goal was last updated.</summary>
    public DateTime UpdatedAt { get; private set; }

    private readonly List<GoalTag> _tags = new();

    /// <summary>The tags attached to this goal.</summary>
    public IReadOnlyCollection<GoalTag> Tags => _tags.AsReadOnly();

    private Goal() { }

    /// <summary>Creates a new goal, requiring a non-empty title.</summary>
    public static Goal Create(string? title, double targetAmount, TradeDate targetDate,
                              string? description, bool achieved, int? categoryId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Goal title is required.");
        return new Goal
        {
            Title = title.Trim(),
            TargetAmount = targetAmount,
            TargetDate = targetDate,
            Description = description ?? "",
            Achieved = achieved,
            CategoryId = categoryId
        };
    }

    /// <summary>Updates the goal's editable fields, requiring a non-empty title.</summary>
    public void Update(string? title, double targetAmount, TradeDate targetDate,
                       string? description, bool achieved, int? categoryId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Goal title is required.");
        Title = title.Trim();
        TargetAmount = targetAmount;
        TargetDate = targetDate;
        Description = description ?? "";
        Achieved = achieved;
        CategoryId = categoryId;
    }
}
