using SmartLawyer.Core.Legal;

namespace SmartLawyer.Core.Tests;

public class RulingReferenceParserTests
{
    [Fact]
    public void FindAll_ParsesStandardReference()
    {
        var r = Assert.Single(RulingReferenceParser.FindAll("وفقاً لما قضت به في الطعن رقم 1234 لسنة 85 ق"));
        Assert.Equal(1234, r.AppealNumber);
        Assert.Equal(85, r.JudicialYear);
        Assert.Null(r.SessionDate);
    }

    [Fact]
    public void FindAll_ParsesEasternDigitsAndSessionDate()
    {
        var r = Assert.Single(RulingReferenceParser.FindAll("الطعن رقم ١٢٣٤ لسنة ٨٥ قضائية - جلسة 14/3/2016"));
        Assert.Equal(1234, r.AppealNumber);
        Assert.Equal(new DateOnly(2016, 3, 14), r.SessionDate);
    }

    [Fact]
    public void FindAll_ParsesShortForm()
    {
        var r = Assert.Single(RulingReferenceParser.FindAll("طعن 55 س 70 ق"));
        Assert.Equal((55, 70), (r.AppealNumber, r.JudicialYear));
    }

    [Fact]
    public void FindAll_FindsMultipleReferences()
    {
        var refs = RulingReferenceParser.FindAll("الطعن رقم 1 لسنة 60 ق، والطعن رقم 2 لسنة 61 ق");
        Assert.Equal([(1, 60), (2, 61)], refs.Select(r => (r.AppealNumber, r.JudicialYear)));
    }

    [Theory]
    [InlineData("2016/3/14", 2016, 3, 14)]
    [InlineData("14-3-2016", 2016, 3, 14)]
    public void ParseDate_AcceptsBothOrders(string input, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), RulingReferenceParser.ParseDate(input));

    [Theory]
    [InlineData("31/2/2016")]
    [InlineData("1/13/2016")]
    [InlineData("2016/3")]
    public void ParseDate_RejectsInvalidDates(string input) =>
        Assert.Null(RulingReferenceParser.ParseDate(input));

    [Fact]
    public void ToString_FormatsReference() =>
        Assert.Equal("الطعن رقم 1234 لسنة 85 ق", new RulingReference(1234, 85, null).ToString());
}
