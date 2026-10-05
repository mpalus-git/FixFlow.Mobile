using System.Xml;
using System.Xml.Linq;

namespace FixFlow.Tools.Comments;

internal sealed class XmlCommentDetector : ICommentDetector
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xaml", ".csproj", ".props", ".targets", ".resx", ".xml", ".slnx", ".manifest", ".appxmanifest", ".svg", ".config",
    };

    public bool Supports(string fileName) => Extensions.Contains(Path.GetExtension(fileName));

    public IReadOnlyList<CommentLocation> Find(string content)
    {
        var document = XDocument.Parse(content, LoadOptions.SetLineInfo);

        return
        [
            .. document.DescendantNodes()
                .OfType<XComment>()
                .Select(comment => new CommentLocation(((IXmlLineInfo)comment).LineNumber, comment.Value.Trim())),
        ];
    }
}
