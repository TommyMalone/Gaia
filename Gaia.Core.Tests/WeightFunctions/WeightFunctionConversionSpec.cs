using Gaia.Core.Factories;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers the conversion flags of every weight function a <see cref="FactorySet" /> registers.</summary>
/// <remarks>
///     Loading refuses a weight function under a consideration whose measurement its flags say it cannot convert. So
///     a flag that disagrees with its Calculate either refuses a pairing that works or loads one that throws on the
///     decision path. Each function is loaded from an element that writes its Type alone.
/// </remarks>
[TestFixture]
public class WeightFunctionConversionSpec
{
    [Test]
    public void SetsEachConvertsFlagExactlyWhenItsCalculateConverts()
    {
        Assert.Multiple(() =>
        {
            foreach (AiString type in new FactorySet().WeightFunctions.Types)
            {
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(
                    $"""<WeightFunction Type="{type.Text}"/>""", new RecordingLogSink());

                Assert.That(
                    creationData.Factories.WeightFunctions.TryCreate(creationData, out WeightFunctionBase? function),
                    Is.True, type.Text);
                Assert.That(function!.ConvertsBool, Is.EqualTo(Converts(() => function.CalculateBool(true))),
                    $"{type.Text} ConvertsBool");
                Assert.That(function.ConvertsInt, Is.EqualTo(Converts(() => function.CalculateInt(1))),
                    $"{type.Text} ConvertsInt");
                Assert.That(function.ConvertsFloat, Is.EqualTo(Converts(() => function.CalculateFloat(1f))),
                    $"{type.Text} ConvertsFloat");
                Assert.That(function.ConvertsString, Is.EqualTo(Converts(() => function.CalculateString("Text"))),
                    $"{type.Text} ConvertsString");
            }
        });
    }

    /// <summary>Reports whether the calculation returns rather than throwing NotSupportedException.</summary>
    private static bool Converts(Action calculate)
    {
        try
        {
            calculate();
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
