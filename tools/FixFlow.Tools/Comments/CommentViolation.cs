namespace FixFlow.Tools.Comments;

internal sealed record CommentViolation(string Path, int Line, string Text);
