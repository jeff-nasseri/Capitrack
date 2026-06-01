namespace Server.Domain.Goals;

/// <summary>Join entity linking a <see cref="Goal"/> to a tag.</summary>
public sealed class GoalTag
{
    public int GoalId { get; set; }
    public int TagId { get; set; }
}

/// <summary>A savings/wealth target with a due date. Aggregate root.</summary>
public sealed class Goal : AggregateRoot<int>
{
    public string Title { get; private set; } = "";
    public double TargetAmount { get; private set; }
    public TradeDate TargetDate { get; private set; } = default!;
    public string Description { get; private set; } = "";
    public bool Achieved { get; private set; }
    public int? CategoryId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<GoalTag> _tags = new();
    public IReadOnlyCollection<GoalTag> Tags => _tags.AsReadOnly();

    private Goal() { }

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
