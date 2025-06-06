using System;
using System.IO;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        var projectPath = @"C:\Path\To\Your\Project";  // ← CHANGE THIS
        var outputPath = Path.Combine(projectPath, "Documentation.txt");

        var sb = new StringBuilder();
        sb.AppendLine("=== Project Documentation ===");
        sb.AppendLine();

        StaticFileDocGenerator.DocumentWwwRoot(Path.Combine(projectPath, "wwwroot"), sb);
        ControllerDocGenerator.DocumentControllersFromSource(Path.Combine(projectPath, "Controllers"), sb);
        RazorPageDocGenerator.DocumentRazorPagesFromSource(Path.Combine(projectPath, "Pages"), sb);

        File.WriteAllText(outputPath, sb.ToString());
        Console.WriteLine("Documentation generated at: " + outputPath);
    }
}
