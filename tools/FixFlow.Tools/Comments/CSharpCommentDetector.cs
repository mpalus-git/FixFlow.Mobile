using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FixFlow.Tools.Comments;

internal sealed class CSharpCommentDetector : ICommentDetector
{
    private static readonly HashSet<SyntaxKind> CommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    public bool Supports(string fileName) =>
        Path.GetExtension(fileName).Equals(".cs", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<CommentLocation> Find(string content)
    {
        var root = CSharpSyntaxTree.ParseText(content).GetRoot();

        return
        [
            .. root.DescendantTrivia(descendIntoTrivia: true)
                .Where(trivia => CommentKinds.Contains(trivia.Kind()))
                .Select(trivia => new CommentLocation(
                    trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    FirstLine(trivia.ToFullString()))),
        ];
    }

    private static string FirstLine(string text) => text.Trim().Split('\n')[0].Trim();
}
