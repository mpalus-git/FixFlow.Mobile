using FixFlow.Tools.Comments;

namespace FixFlow.Tools.Tests.Comments;

public sealed class XmlCommentDetectorTests
{
    private readonly XmlCommentDetector _detector = new();

    [Fact]
    public void Should_Find_Comment_With_Line_When_Element_Contains_Comment()
    {
        var comments = _detector.Find("<ContentPage>\n  <Label />\n  <!-- hidden -->\n</ContentPage>\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(3, "hidden"));
    }

    [Fact]
    public void Should_Find_Comment_When_It_Precedes_Root_Element()
    {
        var comments = _detector.Find("<?xml version=\"1.0\"?>\n<!-- header -->\n<Project />\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(2, "header"));
    }

    [Fact]
    public void Should_Ignore_Dashes_When_They_Are_Part_Of_Attribute_Value()
    {
        var comments = _detector.Find("<Label Text=\"a -- b\" />");

        comments.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("MainPage.xaml", true)]
    [InlineData("FixFlow.Mobile.csproj", true)]
    [InlineData("AppResources.resx", true)]
    [InlineData("AndroidManifest.xml", true)]
    [InlineData("Directory.Packages.props", true)]
    [InlineData("FixFlow.Mobile.slnx", true)]
    [InlineData("Program.cs", false)]
    public void Should_Support_Xml_Based_Files_When_Checking_File_Name(string fileName, bool expected)
    {
        _detector.Supports(fileName).ShouldBe(expected);
    }
}
