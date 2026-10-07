using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class AddIfBraces : CSharpSyntaxRewriter
{
    public int Count { get; private set; }

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        var updated = (IfStatementSyntax)base.VisitIfStatement(node)!;
        if (updated.Statement is not BlockSyntax)
        {
            updated = updated.WithStatement(Wrap(updated.Statement));
        }
        return updated;
    }

    public override SyntaxNode? VisitElseClause(ElseClauseSyntax node)
    {
        var updated = (ElseClauseSyntax)base.VisitElseClause(node)!;
        // Keep else-if chains readable; each branch body is handled by VisitIfStatement.
        if (updated.Statement is not (BlockSyntax or IfStatementSyntax))
        {
            updated = updated.WithStatement(Wrap(updated.Statement));
        }
        return updated;
    }

    private BlockSyntax Wrap(StatementSyntax statement)
    {
        Count++;
        return SyntaxFactory.Block(statement.WithoutLeadingTrivia().WithoutTrailingTrivia())
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(statement.GetTrailingTrivia());
    }
}
