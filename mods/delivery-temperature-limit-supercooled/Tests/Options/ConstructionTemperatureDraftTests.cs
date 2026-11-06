using System;
using System.Globalization;
using DeliveryTemperatureLimit;

namespace DeliveryTemperatureLimit.Tests.Options;
public sealed class ConstructionTemperatureDraftTests
{
    [Test]
    public async Task Validate_WhenInitial_IsValidAndClean()
    {
        var draft = CreateDraft(223, 318);
        await Assert.That(draft.IsDirty).IsFalse();
        await AssertValid(draft, 223, 318);
    }

    [Test]
    public async Task Validate_WhenInvalidNumberEntered_FailsWithoutOverwritingUserDraft()
    {
        var draft = CreateDraft(223, 318);

        foreach (string text in new[] { "", "-", "abc", "2147483648", "1.5" })
        {
            draft.LowerText = text;
            await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.LowerNumber);
            await Assert.That(draft.LowerText).IsEqualTo(text).Because("Validation must not overwrite the draft while typing.");
        }
    }

    [Test]
    public async Task Validate_WhenBoundsViolatedOrReverted_ReportsExpectedErrors()
    {
        var draft = CreateDraft(223, 318);

        draft.LowerText = "-1";
        await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.LowerRange);

        draft.Revert();
        await Assert.That(draft.IsDirty).IsFalse();

        draft.UpperText = "10001";
        await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.UpperRange);

        draft.UpperText = "-";
        await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.UpperNumber);

        draft.UpperText = "223";
        await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.EmptyRange);

        draft.UpperText = "222";
        await Assert.That(draft.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.EmptyRange);

        draft.Reset(0, 10000);
        await AssertValid(draft, 0, 10000);
        await Assert.That(draft.IsDirty).IsFalse();
    }

    [Test]
    public async Task Validate_WhenTextIsUnchanged_DoesNotRunTemperatureConversion()
    {
        int conversions = 0;
        var preserved = new ConstructionTemperatureDraft(
            223, 318,
            value => { conversions++; return value; },
            Format);

        await AssertValid(preserved, 223, 318);
        await Assert.That(conversions).IsEqualTo(0).Because("Initial unedited text must not trigger conversion.");

        preserved.LowerText = "224";
        await AssertValid(preserved, 224, 318);
        await Assert.That(conversions).IsEqualTo(1).Because("Editing lower bound must trigger exactly one conversion.");
    }

    [Test]
    public async Task Validate_WhenConversionProducesOverflowOrNaN_ReportsRangeError()
    {
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.MaxValue })
        {
            var overflow = new ConstructionTemperatureDraft(223, 318, _ => invalid, Format);
            overflow.LowerText = "224";
            await Assert.That(overflow.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.LowerRange);
        }

        var rounding = new ConstructionTemperatureDraft(223, 318, _ => 317.6f, Format);
        rounding.LowerText = "317";
        await Assert.That(rounding.Validate(out _, out _)).IsEqualTo(ConstructionRangeError.EmptyRange);
    }

    [Test]
    public async Task Validate_AcrossAllNineUnitTransitions_PreservesCanonicalKelvinBounds()
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

                await AssertValid(converted, 223, 318);
                await Assert.That(converted.IsDirty).IsFalse();
            }
        }
    }

    private static ConstructionTemperatureDraft CreateDraft(int low, int high) =>
        new ConstructionTemperatureDraft(low, high, value => value, Format);

    [Test]
    public async Task Save_WhenDisabledWithIncompleteInput_PreservesSavedBoundsAndRetainsTyping()
    {
        var draft = CreateDraft(223, 318);
        draft.LowerText = "-";
        await Assert.That(draft.ResolveForSave(false, out int low, out int high)).IsEqualTo(ConstructionRangeError.None);
        await Assert.That(low).IsEqualTo(223);
        await Assert.That(high).IsEqualTo(318);
        await Assert.That(draft.LowerText).IsEqualTo("-");
        await Assert.That(draft.ResolveForSave(true, out _, out _)).IsEqualTo(ConstructionRangeError.LowerNumber).Because("Re-enabling must still validate the retained input.");
    }

    [Test]
    public async Task Save_WhenDisabledWithValidEdits_KeepsTheNewDefaultRange()
    {
        var draft = CreateDraft(223, 318);
        draft.LowerText = "250";
        await Assert.That(draft.ResolveForSave(false, out int low, out int high)).IsEqualTo(ConstructionRangeError.None);
        await Assert.That(low).IsEqualTo(250);
        await Assert.That(high).IsEqualTo(318);
    }

    [Test]
    public async Task Save_WhenDisabledWithAnInvalidExistingRange_DoesNotRequireHiddenRepairs()
    {
        var draft = CreateDraft(318, 223);
        await Assert.That(draft.ResolveForSave(false, out int low, out int high)).IsEqualTo(ConstructionRangeError.None);
        await Assert.That(low).IsEqualTo(318);
        await Assert.That(high).IsEqualTo(223);
        await Assert.That(draft.ResolveForSave(true, out _, out _)).IsEqualTo(ConstructionRangeError.EmptyRange);
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static float ToKelvin(int value, string unit) =>
        unit == "celsius" ? value + 273.15f :
        unit == "fahrenheit" ? (value - 32) * (5f / 9f) + 273.15f : value;

    private static string FromKelvin(int value, string unit) => Math.Round(
        unit == "celsius" ? value - 273.15f :
        unit == "fahrenheit" ? (value - 273.15f) * (9f / 5f) + 32 : value)
        .ToString(CultureInfo.InvariantCulture);

    private static async Task AssertValid(ConstructionTemperatureDraft draft, int expectedLow, int expectedHigh)
    {
        await Assert.That(draft.Validate(out int actualLow, out int actualHigh)).IsEqualTo(ConstructionRangeError.None);
        await Assert.That(actualLow).IsEqualTo(expectedLow);
        await Assert.That(actualHigh).IsEqualTo(expectedHigh);
    }
}
