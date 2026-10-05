namespace FixFlow.Tools.Comments;

internal sealed class CommentChecker(IReadOnlyList<ICommentDetector> detectors)
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "bin", "obj", "TestResults", "artifacts",
    };

    public static CommentChecker CreateDefault() =>
        new([new CSharpCommentDetector(), new XmlCommentDetector(), new HashCommentDetector()]);

    public IReadOnlyList<CommentViolation> Check(string rootDirectory)
    {
        var violations = new List<CommentViolation>();

        foreach (var file in EnumerateFiles(rootDirectory))
        {
            var detector = detectors.FirstOrDefault(candidate => candidate.Supports(file));
            if (detector is null)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDirectory, file).Replace('\\', '/');
            violations.AddRange(detector.Find(File.ReadAllText(file))
                .Select(comment => new CommentViolation(relativePath, comment.Line, comment.Text)));
        }

        return violations;
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            yield return file;
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(subdirectory)))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(subdirectory))
            {
                yield return file;
            }
        }
    }
}
