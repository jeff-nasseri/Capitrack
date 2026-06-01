namespace Client.Application.Models;

/// <summary>Mutable model the goal modal form binds to; read back on save to build the API request.</summary>
public class GoalFormModel
{
    public int? Id { get; set; }
    public string Title { get; set; } = "";
    public double? TargetAmount { get; set; }
    public string TargetDate { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Achieved { get; set; }
    public HashSet<int> TagIds { get; set; } = [];
}
