using FixFlow.Tools.Comments;

namespace FixFlow.Tools.Tests.Comments;

public sealed class CSharpCommentDetectorTests
{
    private readonly CSharpCommentDetector _detector = new();

    [Fact]
    public void Should_Find_Single_Line_Comment_When_Code_Contains_Double_Slash()
    {
        var comments = _detector.Find("var x = 1;\nvar y = 2; // second\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(2, "// second"));
    }

    [Fact]
    public void Should_Find_Block_Comment_When_Code_Contains_Multi_Line_Comment()
    {
        var comments = _detector.Find("/* first\n   still first */\nclass A { }\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(1, "/* first"));
    }

    [Fact]
    public void Should_Find_Documentation_Comment_When_Member_Has_Triple_Slash()
    {
        var comments = _detector.Find("class A\n{\n    /// <summary>Docs</summary>\n    void M() { }\n}\n");

        comments.ShouldHaveSingleItem().Line.ShouldBe(3);
    }

    [Fact]
    public void Should_Ignore_Comment_Markers_When_They_Are_Inside_String_Literals()
    {
        var comments = _detector.Find("var url = \"https://example.com\";\nvar text = \"/* not a comment */\";\n");

        comments.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Program.cs", true)]
    [InlineData("App.xaml", false)]
    [InlineData("ci.yml", false)]
    public void Should_Support_Only_CSharp_Files_When_Checking_File_Name(string fileName, bool expected)
    {
        _detector.Supports(fileName).ShouldBe(expected);
    }
}
