using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class RazorPageDocGenerator
{
    public static void DocumentRazorPagesFromSource(string pagesPath, StringBuilder sb)
    {
        sb.AppendLine("== Razor Pages ==");

        foreach (var file in Directory.GetFiles(pagesPath, "*.cshtml.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(pagesPath, file).Replace("\\", "/");
            sb.AppendLine($"Page: {relativePath}");

            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(cls => cls.BaseList?.Types.Any(bt => bt.ToString().Contains("PageModel")) ?? false);

            foreach (var cls in classes)
            {
                var authAttr = cls.AttributeLists.SelectMany(a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString() == "Authorize");

                if (authAttr != null)
                {
                    var args = authAttr.ArgumentList?.Arguments.ToString();
                    sb.AppendLine($"  [Authorize{(args != null ? $" {args}" : "")}]");
                }

                var methods = cls.Members.OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.Text.StartsWith("On"));

                foreach (var method in methods)
                {
                    sb.Append($"  Handler: {method.Identifier.Text}");

                    var mAuthAttr = method.AttributeLists.SelectMany(a => a.Attributes)
                        .FirstOrDefault(attr => attr.Name.ToString() == "Authorize");

                    if (mAuthAttr != null)
                    {
                        var args = mAuthAttr.ArgumentList?.Arguments.ToString();
                        sb.Append($" [Authorize{(args != null ? $" {args}" : "")}]");
                    }

                    sb.AppendLine();

                    foreach (var param in method.ParameterList.Parameters)
                    {
                        sb.AppendLine($"    - {param.Identifier.Text} : {param.Type?.ToString()}");
                    }
                }

                sb.AppendLine();
            }
        }
    }
}
