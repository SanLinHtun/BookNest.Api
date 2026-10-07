using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var rootDirectory = args[0];
if (args.Contains("--if-braces"))
{
    var added = 0;
    foreach (var directory in new[] { "BookNest.Api", "BookNest.Verification" })
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(rootDirectory, directory), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
        {
            var original = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var rewriter = new AddIfBraces();
            var updated = rewriter.Visit(original)!;
            if (rewriter.Count == 0)
            {
                continue;
            }
            // The only allowed token changes are inserted block braces.
            var before = original.DescendantTokens().Select(t => t.RawKind + ":" + t.Text).ToArray();
            var after = updated.DescendantTokens().Where(t => !t.IsKind(SyntaxKind.OpenBraceToken) && !t.IsKind(SyntaxKind.CloseBraceToken))
                .Select(t => t.RawKind + ":" + t.Text).ToArray();
            var beforeWithoutBraces = original.DescendantTokens().Where(t => !t.IsKind(SyntaxKind.OpenBraceToken) && !t.IsKind(SyntaxKind.CloseBraceToken))
                .Select(t => t.RawKind + ":" + t.Text).ToArray();
            if (!beforeWithoutBraces.SequenceEqual(after))
            {
                throw new Exception($"Unexpected token change in {file}");
            }
            File.WriteAllText(file, updated.NormalizeWhitespace("    ", "\r\n").ToFullString());
            added += rewriter.Count;
        }
    }
    Console.WriteLine($"Added braces to {added} if/else branch bodies.");
}
var changed = 0;
foreach (var directory in new[] { "BookNest.Api", "BookNest.Verification" })
{
    foreach (var file in Directory.EnumerateFiles(Path.Combine(rootDirectory, directory), "*.cs", SearchOption.AllDirectories)
        .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
    {
        var source = File.ReadAllText(file);
        var original = CSharpSyntaxTree.ParseText(source).GetRoot();
        var tokens = original.DescendantTokens().ToArray();
        var positions = new HashSet<int>();
        bool SameLine(int start, int end) => !source[start..end].Contains('\n');
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.Parent is InterpolationSyntax)
                continue;
            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                if (i > 0 && SameLine(tokens[i - 1].Span.End, token.SpanStart))
                    positions.Add(token.SpanStart);
                if (i + 1 < tokens.Length && SameLine(token.Span.End, tokens[i + 1].SpanStart))
                    positions.Add(token.Span.End);
            }
            else if (token.IsKind(SyntaxKind.CloseBraceToken))
            {
                if (i > 0 && SameLine(tokens[i - 1].Span.End, token.SpanStart))
                    positions.Add(token.SpanStart);
                if (i + 1 < tokens.Length && tokens[i + 1].Kind() is not (SyntaxKind.SemicolonToken or SyntaxKind.CommaToken
                    or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.DotToken or SyntaxKind.EqualsToken)
                    && SameLine(token.Span.End, tokens[i + 1].SpanStart))
                    positions.Add(token.Span.End);
            }
            else if (token.IsKind(SyntaxKind.SemicolonToken) && token.Parent is not ForStatementSyntax)
            {
                if (i + 1 < tokens.Length && SameLine(token.Span.End, tokens[i + 1].SpanStart))
                    positions.Add(token.Span.End);
            }
            else if (token.IsKind(SyntaxKind.CommaToken) && token.Parent is InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax or SwitchExpressionSyntax)
            {
                if (i + 1 < tokens.Length && SameLine(token.Span.End, tokens[i + 1].SpanStart))
                    positions.Add(token.Span.End);
            }
        }
        var formatted = source;
        foreach (var position in positions.OrderByDescending(x => x))
            formatted = formatted.Insert(position, "\r\n");
        var rewritten = CSharpSyntaxTree.ParseText(formatted).GetRoot();
        if (!SyntaxFactory.AreEquivalent(original, rewritten))
            throw new Exception($"Formatting changed syntax in {file}");
        if (formatted != source)
        {
            File.WriteAllText(file, formatted);
            changed++;
        }
    }
}
Console.WriteLine($"Expanded line breaks in {changed} files; all syntax trees remained equivalent.");
