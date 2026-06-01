namespace Server.Domain.Goals;

/// <summary>Join entity linking a <see cref="Goal"/> to a tag.</summary>
public sealed class GoalTag
{
    /// <summary>The linked goal's identifier.</summary>
    public int GoalId { get; set; }

    /// <summary>The linked tag's identifier.</summary>
    public int TagId { get; set; }
}
