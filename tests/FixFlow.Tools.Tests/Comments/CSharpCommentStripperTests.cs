using FixFlow.Tools.Comments;

namespace FixFlow.Tools.Tests.Comments;

public sealed class CSharpCommentStripperTests
{
    [Fact]
    public void Should_Remove_Whole_Line_When_Line_Contains_Only_Comment()
    {
        var result = CSharpCommentStripper.Strip("var x = 1;\n    // note\nvar y = 2;\n");

        result.ShouldBe("var x = 1;\nvar y = 2;\n");
    }

    [Fact]
    public void Should_Keep_Code_Without_Trailing_Space_When_Comment_Ends_Line()
    {
        var result = CSharpCommentStripper.Strip("var y = 2; // second\n");

        result.ShouldBe("var y = 2;\n");
    }

    [Fact]
    public void Should_Remove_Documentation_And_Block_Comments_When_They_Span_Lines()
    {
        var source = "class A\n{\n    /// <summary>\n    /// Docs\n    /// </summary>\n    /* block\n       comment */\n    void M() { }\n}\n";

        var result = CSharpCommentStripper.Strip(source);

        result.ShouldBe("class A\n{\n    void M() { }\n}\n");
    }

    [Fact]
    public void Should_Keep_Directives_And_Strings_When_Stripping_Comments()
    {
        var source = "#nullable enable annotations\nvar url = \"https://example.com\"; // link\n";

        var result = CSharpCommentStripper.Strip(source);

        result.ShouldBe("#nullable enable annotations\nvar url = \"https://example.com\";\n");
    }

    [Fact]
    public void Should_Collapse_Blank_Lines_When_Removed_Comment_Was_Between_Them()
    {
        var source = "// header\n\nusing System;\n\n// section\n\nclass A { }\n";

        var result = CSharpCommentStripper.Strip(source);

        result.ShouldBe("using System;\n\nclass A { }\n");
    }

    [Fact]
    public void Should_Normalize_Line_Endings_When_Source_Uses_Crlf()
    {
        var result = CSharpCommentStripper.Strip("var x = 1;\r\n// note\r\nvar y = 2;\r\n");

        result.ShouldBe("var x = 1;\nvar y = 2;\n");
    }

    [Fact]
    public void Should_Leave_No_Comments_When_Result_Is_Checked_By_Detector()
    {
        var source = "/// <summary>A</summary>\nclass A { int x; /* inline */ }\n";

        new CSharpCommentDetector().Find(CSharpCommentStripper.Strip(source)).ShouldBeEmpty();
    }
}
