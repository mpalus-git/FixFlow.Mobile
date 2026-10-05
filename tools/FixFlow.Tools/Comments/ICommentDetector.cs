namespace FixFlow.Tools.Comments;

internal interface ICommentDetector
{
    bool Supports(string fileName);

    IReadOnlyList<CommentLocation> Find(string content);
}
