using System;
using System.Globalization;
using DeliveryTemperatureLimit;

namespace DeliveryTemperatureLimit.Tests.Options;

[TestClass]
public sealed class ConstructionTemperatureDraftTests
{
    [TestMethod]
    public void Validate_WhenInitial_IsValidAndClean()
    {
        var draft = CreateDraft(223, 318);
        Assert.IsFalse(draft.IsDirty);
        AssertValid(draft, 223, 318);
    }

    [TestMethod]
    public void Validate_WhenInvalidNumberEntered_FailsWithoutOverwritingUserDraft()
    {
        var draft = CreateDraft(223, 318);

        foreach (string text in new[] { "", "-", "abc", "2147483648", "1.5" })
        {
            draft.LowerText = text;
            Assert.AreEqual(ConstructionRangeError.LowerNumber, draft.Validate(out _, out _));
            Assert.AreEqual(text, draft.LowerText, "Validation must not overwrite the draft while typing.");
        }
    }

    [TestMethod]
    public void Validate_WhenBoundsViolatedOrReverted_ReportsExpectedErrors()
    {
        var draft = CreateDraft(223, 318);

        draft.LowerText = "-1";
        Assert.AreEqual(ConstructionRangeError.LowerRange, draft.Validate(out _, out _));

        draft.Revert();
        Assert.IsFalse(draft.IsDirty);

        draft.UpperText = "10001";
        Assert.AreEqual(ConstructionRangeError.UpperRange, draft.Validate(out _, out _));

        draft.UpperText = "-";
        Assert.AreEqual(ConstructionRangeError.UpperNumber, draft.Validate(out _, out _));

        draft.UpperText = "223";
        Assert.AreEqual(ConstructionRangeError.EmptyRange, draft.Validate(out _, out _));

        draft.UpperText = "222";
        Assert.AreEqual(ConstructionRangeError.EmptyRange, draft.Validate(out _, out _));

        draft.Reset(0, 10000);
        AssertValid(draft, 0, 10000);
        Assert.IsFalse(draft.IsDirty);
    }

    [TestMethod]
    public void Validate_WhenTextIsUnchanged_DoesNotRunTemperatureConversion()
    {
        int conversions = 0;
        var preserved = new ConstructionTemperatureDraft(
            223, 318,
            value => { conversions++; return value; },
            Format);

        AssertValid(preserved, 223, 318);
        Assert.AreEqual(0, conversions, "Initial unedited text must not trigger conversion.");

        preserved.LowerText = "224";
        AssertValid(preserved, 224, 318);
        Assert.AreEqual(1, conversions, "Editing lower bound must trigger exactly one conversion.");
    }

    [TestMethod]
    public void Validate_WhenConversionProducesOverflowOrNaN_ReportsRangeError()
    {
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.MaxValue })
        {
            var overflow = new ConstructionTemperatureDraft(223, 318, _ => invalid, Format);
            overflow.LowerText = "224";
            Assert.AreEqual(ConstructionRangeError.LowerRange, overflow.Validate(out _, out _));
        }

        var rounding = new ConstructionTemperatureDraft(223, 318, _ => 317.6f, Format);
        rounding.LowerText = "317";
        Assert.AreEqual(ConstructionRangeError.EmptyRange, rounding.Validate(out _, out _));
    }

    [TestMethod]
    public void Validate_AcrossAllNineUnitTransitions_PreservesCanonicalKelvinBounds()
    {
        foreach (string source in new[] { "celsius", "fahrenheit", "kelvin" })
        {
            foreach (string display in new[] { "celsius", "fahrenheit", "kelvin" })
            {
                int lower = source == "celsius" ? -50 : source == "fahrenheit" ? -58 : 223;
                int upper = source == "celsius" ? 45 : source == "fahrenheit" ? 113 : 318;
                var converted = new ConstructionTemperatureDraft(
                    (int)Math.Round(ToKelvin(lower, source)),
                    (int)Math.Round(ToKelvin(upper, source)),
                    value => ToKelvin(value, display),
                    value => FromKelvin(value, display));

                AssertValid(converted, 223, 318);
                Assert.IsFalse(converted.IsDirty);
            }
        }
    }

    private static ConstructionTemperatureDraft CreateDraft(int low, int high) =>
        new ConstructionTemperatureDraft(low, high, value => value, Format);

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static float ToKelvin(int value, string unit) =>
        unit == "celsius" ? value + 273.15f :
        unit == "fahrenheit" ? (value - 32) * (5f / 9f) + 273.15f : value;

    private static string FromKelvin(int value, string unit) => Math.Round(
        unit == "celsius" ? value - 273.15f :
        unit == "fahrenheit" ? (value - 273.15f) * (9f / 5f) + 32 : value)
        .ToString(CultureInfo.InvariantCulture);

    private static void AssertValid(ConstructionTemperatureDraft draft, int expectedLow, int expectedHigh)
    {
        Assert.AreEqual(ConstructionRangeError.None, draft.Validate(out int actualLow, out int actualHigh));
        Assert.AreEqual(expectedLow, actualLow);
        Assert.AreEqual(expectedHigh, actualHigh);
    }
}
