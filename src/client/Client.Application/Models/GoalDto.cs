namespace Client.Application.Models;

/// <summary>A savings/financial goal with a target amount and date.</summary>
public class GoalDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public double TargetAmount { get; set; }
    public string TargetDate { get; set; } = "";
    public string Description { get; set; } = "";
    public int Achieved { get; set; }
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}
