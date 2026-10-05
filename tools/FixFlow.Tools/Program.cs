using FixFlow.Tools.Comments;

if (args is not ["check-comments"] and not ["check-comments", _])
{
    Console.Error.WriteLine("Usage: FixFlow.Tools check-comments [directory]");
    return 2;
}

var rootDirectory = args.Length == 2 ? args[1] : Directory.GetCurrentDirectory();
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
