using Server.Infrastructure.Services.Import;

namespace Server.Tests;

public class DecimalParserTests
{
    [Theory]
    [InlineData("0.00107319", DecimalSeparator.Point, "0.00107319")]
    [InlineData("1,284.75", DecimalSeparator.Point, "1284.75")]
    [InlineData("\"1,284.75\"", DecimalSeparator.Point, "1284.75")]
    [InlineData("1,234,567.891", DecimalSeparator.Point, "1234567.891")]
    [InlineData("1.284,75", DecimalSeparator.Comma, "1284.75")]
    [InlineData("1.234.567,891", DecimalSeparator.Comma, "1234567.891")]
    [InlineData("0,5", DecimalSeparator.Comma, "0.5")]
    [InlineData("0,5", DecimalSeparator.Auto, "0.5")]
    [InlineData("1.305", DecimalSeparator.Comma, "1305")]
    [InlineData("1,305", DecimalSeparator.Point, "1305")]
    [InlineData("12 345,67", DecimalSeparator.Comma, "12345.67")]
    [InlineData("12 345,67", DecimalSeparator.Comma, "12345.67")]
    [InlineData("1'234.50", DecimalSeparator.Point, "1234.50")]
    [InlineData("$1,234.56", DecimalSeparator.Point, "1234.56")]
    [InlineData("USD 150.23", DecimalSeparator.Point, "150.23")]
    [InlineData("€ 3,50", DecimalSeparator.Comma, "3.50")]
    [InlineData("0.5 BTC", DecimalSeparator.Point, "0.5")]
    [InlineData("-0.1", DecimalSeparator.Point, "-0.1")]
    [InlineData("(12.5)", DecimalSeparator.Point, "-12.5")]
    [InlineData("−3", DecimalSeparator.Point, "-3")]
    [InlineData("1E-8", DecimalSeparator.Point, "0.00000001")]
    [InlineData("0.000000000000000001", DecimalSeparator.Point, "0.000000000000000001")]
    [InlineData("﻿42", DecimalSeparator.Point, "42")]
    public void Parses_locale_and_format_variants_exactly(string text, DecimalSeparator separator, string expected)
    {
        DecimalParser.TryParse(text, separator, out var value).Should().BeTrue();
        value.Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("ID 0")]      // an NFT token id — not a quantity, must not become 0
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("abc")]
    [InlineData("1.2.3,4,5")]
    public void Rejects_values_that_are_not_numbers(string text)
    {
        DecimalParser.TryParse(text, DecimalSeparator.Point, out _).Should().BeFalse();
    }

    [Fact]
    public void Detects_point_decimals_from_a_file_with_comma_grouping()
    {
        DecimalParser.Detect(["0.00107319", "\"1,284.75\"", "73.18", ""]).Should().Be(DecimalSeparator.Point);
    }

    [Fact]
    public void Detects_comma_decimals()
    {
        DecimalParser.Detect(["0,5", "1.284,75", "12,75"]).Should().Be(DecimalSeparator.Comma);
    }

    [Fact]
    public void Ambiguous_thousands_only_values_default_to_point()
    {
        DecimalParser.Detect(["1,305", "2,500"]).Should().Be(DecimalSeparator.Point);
    }
}
