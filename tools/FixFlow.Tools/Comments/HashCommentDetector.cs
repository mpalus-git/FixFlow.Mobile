namespace FixFlow.Tools.Comments;

internal sealed class HashCommentDetector : ICommentDetector
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".yml", ".yaml" };

    private static readonly HashSet<string> FileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gitignore", ".gitattributes", ".editorconfig",
    };

    public bool Supports(string fileName) =>
        FileNames.Contains(Path.GetFileName(fileName)) || Extensions.Contains(Path.GetExtension(fileName));

    public IReadOnlyList<CommentLocation> Find(string content)
    {
        var lines = content.Split('\n');
        var comments = new List<CommentLocation>();

        for (var index = 0; index < lines.Length; index++)
        {
            var start = FindCommentStart(lines[index]);
            if (start >= 0)
            {
                comments.Add(new CommentLocation(index + 1, lines[index][start..].Trim()));
            }
        }

        return comments;
    }

    private static int FindCommentStart(string line)
    {
        char? openQuote = null;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (openQuote is not null)
            {
                if (character == '\\' && openQuote == '"')
                {
                    index++;
                }
                else if (character == openQuote)
                {
                    openQuote = null;
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                openQuote = character;
            }
            else if (character == '#' && (index == 0 || char.IsWhiteSpace(line[index - 1])))
            {
                return index;
            }
        }

        return -1;
    }
}
