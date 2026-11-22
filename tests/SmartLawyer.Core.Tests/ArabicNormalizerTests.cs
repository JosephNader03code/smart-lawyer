using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Tests;

public class ArabicNormalizerTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إبراهيم", "ابراهيم")]
    [InlineData("آمال", "امال")]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("المحكمة", "المحكمه")]
    [InlineData("مسؤولية", "مسووليه")]
    [InlineData("قائمة", "قايمه")]
    public void NormalizeForSearch_UnifiesLetterForms(string input, string expected) =>
        Assert.Equal(expected, ArabicNormalizer.NormalizeForSearch(input));

    [Fact]
    public void NormalizeForSearch_RemovesDiacriticsAndTatweel() =>
        Assert.Equal("محكمه النقض", ArabicNormalizer.NormalizeForSearch("مَحْكَمَـــةُ النَّقْضِ"));

    [Fact]
    public void NormalizeForSearch_CollapsesWhitespaceAndTrims() =>
        Assert.Equal("ماده 147", ArabicNormalizer.NormalizeForSearch("  مادة \t\n ١٤٧  "));

    [Theory]
    [InlineData("١٢٣٤", "1234")]
    [InlineData("۱۲۳", "123")]
    [InlineData("سنة ٨٥ ق", "سنة 85 ق")]
    public void NormalizeDigits_ConvertsEasternDigits(string input, string expected) =>
        Assert.Equal(expected, ArabicNormalizer.NormalizeDigits(input));

    [Fact]
    public void NormalizeDigits_KeepsLetters() =>
        Assert.Equal("المحكمة", ArabicNormalizer.NormalizeDigits("المحكمة"));

    [Fact]
    public void NormalizeForSearch_IsIdempotent()
    {
        var once = ArabicNormalizer.NormalizeForSearch("إخطار القوة القاهرة في ١٣ أغسطس");
        Assert.Equal(once, ArabicNormalizer.NormalizeForSearch(once));
    }
}
