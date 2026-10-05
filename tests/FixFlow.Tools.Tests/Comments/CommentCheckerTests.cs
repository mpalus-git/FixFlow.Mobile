using FixFlow.Tools.Comments;

namespace FixFlow.Tools.Tests.Comments;

public sealed class CommentCheckerTests : IDisposable
{
    private readonly string _rootDirectory = Directory.CreateTempSubdirectory("fixflow-tools-").FullName;

    [Fact]
    public void Should_Report_Relative_Path_And_Line_When_File_Contains_Comment()
    {
        WriteFile("src/App/Program.cs", "var x = 1;\n// note\n");

        var violations = CommentChecker.CreateDefault().Check(_rootDirectory);

        violations.ShouldHaveSingleItem().ShouldBe(new CommentViolation("src/App/Program.cs", 2, "// note"));
    }

    [Fact]
    public void Should_Skip_File_When_It_Is_In_Build_Output_Directory()
    {
        WriteFile("src/App/obj/Generated.cs", "// generated\n");
        WriteFile("src/App/bin/Debug/App.xml", "<doc><!-- generated --></doc>");

        CommentChecker.CreateDefault().Check(_rootDirectory).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Skip_File_When_No_Detector_Supports_It()
    {
        WriteFile("README.md", "# FixFlow.Mobile\n");

        CommentChecker.CreateDefault().Check(_rootDirectory).ShouldBeEmpty();
    }

    public void Dispose() => Directory.Delete(_rootDirectory, recursive: true);

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_rootDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
