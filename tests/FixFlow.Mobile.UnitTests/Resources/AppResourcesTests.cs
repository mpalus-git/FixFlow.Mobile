using System.Collections;
using System.Globalization;
using FixFlow.Mobile.Core.Resources;

namespace FixFlow.Mobile.UnitTests.Resources;

public sealed class AppResourcesTests
{
    [Fact]
    public void Should_Have_Non_Empty_English_Text_When_Polish_Key_Exists()
    {
        var polish = ReadResourceSet(CultureInfo.InvariantCulture);
        var english = ReadResourceSet(CultureInfo.GetCultureInfo("en"));

        polish.ShouldNotBeEmpty();
        english.Keys.ShouldBe(polish.Keys, ignoreOrder: true);
        english.Values.ShouldAllBe(value => !string.IsNullOrWhiteSpace(value));
    }

    private static Dictionary<string, string> ReadResourceSet(CultureInfo culture)
    {
        var resourceSet = AppResources.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"Missing resources for culture '{culture.Name}'.");

        return resourceSet.Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
