using FixFlow.Tools.Comments;

return args switch
{
    ["check-comments"] => CheckComments(Directory.GetCurrentDirectory()),
    ["check-comments", var directory] => CheckComments(directory),
    ["strip-comments", var file] => StripComments(file),
    _ => PrintUsage(),
};

static int CheckComments(string rootDirectory)
{
    var violations = CommentChecker.CreateDefault().Check(rootDirectory);

    foreach (var violation in violations)
    {
        Console.WriteLine($"{violation.Path}:{violation.Line}: {violation.Text}");
    }

    if (violations.Count > 0)
    {
        Console.WriteLine($"Found {violations.Count} comment(s).");
        return 1;
    }

    Console.WriteLine("No comments found.");
    return 0;
}

static int StripComments(string file)
{
    File.WriteAllText(file, CSharpCommentStripper.Strip(File.ReadAllText(file)));
    Console.WriteLine($"Stripped comments from {file}");
    return 0;
}

static int PrintUsage()
{
    Console.Error.WriteLine("Usage: FixFlow.Tools check-comments [directory] | strip-comments <file.cs>");
    return 2;
}
