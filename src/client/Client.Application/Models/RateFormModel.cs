namespace Client.Application.Models;

/// <summary>Mutable model the currency-rate modal form binds to; read back on save to build the API request.</summary>
public class RateFormModel
{
    public int? Id { get; set; }
    public string FromCurrency { get; set; } = "";
    public string ToCurrency { get; set; } = "";
    public double? Rate { get; set; }
}
