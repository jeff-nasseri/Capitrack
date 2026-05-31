namespace Capitrack.Web.Models;

// Mutable models the modal forms bind to. Pages create one, show the form,
// and on save read it back to build the API request.

public class TransactionFormModel
{
    public int? Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public double? Quantity { get; set; }
    public double? Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Notes { get; set; } = "";
    public HashSet<int> TagIds { get; set; } = [];
}

public class AccountFormModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "general";
    public string Currency { get; set; } = "EUR";
    public string Icon { get; set; } = "wallet";
    public string Color { get; set; } = "#6366f1";
    public string Description { get; set; } = "";
    public HashSet<int> TagIds { get; set; } = [];
}

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

public class TagFormModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
}

public class RateFormModel
{
    public int? Id { get; set; }
    public string FromCurrency { get; set; } = "";
    public string ToCurrency { get; set; } = "";
    public double? Rate { get; set; }
}
