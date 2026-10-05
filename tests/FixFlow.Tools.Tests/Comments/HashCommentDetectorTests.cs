using FixFlow.Tools.Comments;

namespace FixFlow.Tools.Tests.Comments;

public sealed class HashCommentDetectorTests
{
    private readonly HashCommentDetector _detector = new();

    [Fact]
    public void Should_Find_Comment_When_Line_Starts_With_Hash()
    {
        var comments = _detector.Find("name: CI\n  # build steps\non: push\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(2, "# build steps"));
    }

    [Fact]
    public void Should_Find_Trailing_Comment_When_Hash_Follows_Whitespace()
    {
        var comments = _detector.Find("runs-on: ubuntu-26.04 # runner\n");

        comments.ShouldHaveSingleItem().ShouldBe(new CommentLocation(1, "# runner"));
    }

    [Theory]
    [InlineData("name: 'build # 1'")]
    [InlineData("run: echo \"# not a comment\"")]
    [InlineData("run: echo \"quoted \\\" # still quoted\"")]
    [InlineData("url: https://example.com/#anchor")]
    [InlineData("run: echo $#")]
    public void Should_Ignore_Hash_When_It_Does_Not_Start_A_Comment(string line)
    {
        _detector.Find(line).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ci.yml", true)]
    [InlineData("docker-compose.yaml", true)]
    [InlineData(".gitignore", true)]
    [InlineData(".gitattributes", true)]
    [InlineData(".editorconfig", true)]
    [InlineData("README.md", false)]
    public void Should_Support_Yaml_And_Git_Config_Files_When_Checking_File_Name(string fileName, bool expected)
    {
        _detector.Supports(fileName).ShouldBe(expected);
    }
}
