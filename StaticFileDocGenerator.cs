using System.IO;
using System.Text;

public static class StaticFileDocGenerator
{
    public static void DocumentWwwRoot(string wwwrootPath, StringBuilder sb)
    {
        sb.AppendLine("== wwwroot Directory Files ==");
        if (!Directory.Exists(wwwrootPath))
        {
            sb.AppendLine("  (No wwwroot directory found)");
            return;
        }

        foreach (var file in Directory.GetFiles(wwwrootPath, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(wwwrootPath, file);
            sb.AppendLine($"- {relativePath.Replace("\\", "/")}");
        }

        sb.AppendLine();
    }
}
