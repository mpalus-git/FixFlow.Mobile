using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FixFlow.Tools.Comments;

internal static class CSharpCommentStripper
{
    private static readonly HashSet<SyntaxKind> CommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    public static string Strip(string content)
    {
        var text = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var removed = MarkCommentCharacters(text);
        var output = new StringBuilder(text.Length);
        var lastEmittedBlank = true;
        var afterDroppedLine = false;
        var lineStart = 0;

        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var hadComment = false;
            var line = new StringBuilder(lineEnd - lineStart);
            for (var index = lineStart; index < lineEnd; index++)
            {
                if (removed[index])
                {
                    hadComment = true;
                }
                else
                {
                    line.Append(text[index]);
                }
            }

            var kept = hadComment ? line.ToString().TrimEnd() : line.ToString();
            var isBlank = string.IsNullOrWhiteSpace(kept);

            if (hadComment && isBlank)
            {
                afterDroppedLine = true;
            }
            else if (!(isBlank && lastEmittedBlank && afterDroppedLine))
            {
                output.Append(kept).Append('\n');
                lastEmittedBlank = isBlank;
                afterDroppedLine &= isBlank;
            }

            lineStart = lineEnd + 1;
        }

        return output.ToString();
    }

    private static bool[] MarkCommentCharacters(string text)
    {
        var removed = new bool[text.Length];
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (!CommentKinds.Contains(trivia.Kind()))
            {
                continue;
            }

            for (var index = trivia.FullSpan.Start; index < trivia.FullSpan.End; index++)
            {
                removed[index] = text[index] != '\n';
            }
        }

        return removed;
    }
}
