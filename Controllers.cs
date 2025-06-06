using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class ControllerDocGenerator
{
    public static void DocumentControllersFromSource(string controllersPath, StringBuilder sb)
    {
        sb.AppendLine("== Controllers ==");
        foreach (var file in Directory.GetFiles(controllersPath, "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(cls => cls.BaseList != null && cls.BaseList.Types
                    .Any(bt => bt.ToString().Contains("Controller")));

            foreach (var cls in classes)
            {
                sb.AppendLine($"Controller: {cls.Identifier.Text}");

                var methods = cls.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    var httpAttr = method.AttributeLists.SelectMany(a => a.Attributes)
                        .FirstOrDefault(attr => attr.Name.ToString().StartsWith("Http"));

                    var authAttr = method.AttributeLists.SelectMany(a => a.Attributes)
                        .FirstOrDefault(attr => attr.Name.ToString() == "Authorize");

                    sb.Append($"  Method: {method.Identifier.Text}");

                    if (httpAttr != null)
                        sb.Append($" [{httpAttr.Name}]");

                    if (authAttr != null)
                    {
                        var args = authAttr.ArgumentList?.Arguments.ToString();
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
