private void setParameters(OpenApiOperation operation, HandlerMethodDescriptor handler)
{
    var nonFormParams = new List<OpenApiParameter>();
    var formParams = new Dictionary<string, OpenApiSchema>();
    var objectParams = new Dictionary<string, OpenApiSchema>();

    foreach (var param in handler.Parameters)
    {
        var paramName = param.Name;
        if (param.BindingInfo?.BinderModelName != null)
        {
            paramName = param.BindingInfo.BinderModelName;
        }

        var paramType = param.ParameterType;
        var bindingSource = param.BindingInfo?.BindingSource;
        string openAPIType = MapClrTypeToOpenApiType(paramType);

        // Generate schema
        OpenApiSchema schema = new OpenApiSchema { Type = openAPIType };
        if (openAPIType == "object" || openAPIType == "array")
        {
            schema = GenerateSchema(param.ParameterType);
        }

        if (bindingSource == BindingSource.Form || bindingSource == BindingSource.Body)
        {
            formParams[paramName] = schema;
        }
        else
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = paramName,
                In = ParameterLocation.Query,
                Required = true,
                Schema = schema
            });
        }
    }

    // Build OpenApiRequestBody from formParams
    if (formParams.Any())
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = formParams,
            Required = new HashSet<string>(formParams.Keys) // Optional: mark all as required
        };

        var content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/x-www-form-urlencoded"] = new OpenApiMediaType
            {
                Schema = schema
            }
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = content,
            Required = true
        };
    }
}
                    
                    
                    
                    
                    
                    // [Route] at method level
                    var methodRouteAttr = method.AttributeLists.SelectMany(a => a.Attributes)
                        .FirstOrDefault(attr => attr.Name.ToString().Contains("Route"));

                    string methodRouteTemplate = methodRouteAttr?.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"');

                    if (methodRouteTemplate != null)
                        methodRouteTemplate = methodRouteTemplate.Replace("[controller]", controllerName)
                                                                 .Replace("[action]", methodName);

                    // Resolve full route
                    string fullRoute = classRouteTemplate;
                    if (!string.IsNullOrEmpty(methodRouteTemplate))
                    {
                        if (!string.IsNullOrEmpty(fullRoute) && !fullRoute.EndsWith("/"))
                            fullRoute += "/";
                        fullRoute += methodRouteTemplate;
                    }





using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

class Program
{
    static void Main(string[] args)
    {
        var projectPath = @"C:\Path\To\Your\Project";
        var outputPath = Path.Combine(projectPath, "Documentation.txt");

        var sb = new StringBuilder();
        sb.AppendLine("=== Project Documentation ===");
        sb.AppendLine();

        DocumentWwwRoot(Path.Combine(projectPath, "wwwroot"), sb);
        DocumentControllers(projectPath, sb);
        DocumentRazorPages(Path.Combine(projectPath, "Pages"), sb);

        File.WriteAllText(outputPath, sb.ToString());
        Console.WriteLine("Documentation generated at: " + outputPath);
    }

    static void DocumentWwwRoot(string wwwrootPath, StringBuilder sb)
    {
        sb.AppendLine("== wwwroot Directory Files ==");
        foreach (var file in Directory.GetFiles(wwwrootPath, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(wwwrootPath, file);
            sb.AppendLine($"- {relativePath.Replace("\\", "/")}");
        }
        sb.AppendLine();
    }

    static void DocumentControllers(string projectPath, StringBuilder sb)
    {
        sb.AppendLine("== Controllers ==");

        var dllPath = Directory.GetFiles(projectPath, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("YourProjectName")); // Replace with actual DLL name

        if (dllPath == null)
        {
            sb.AppendLine("No compiled DLL found.");
            return;
        }

        var assembly = Assembly.LoadFrom(dllPath);
        var controllers = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) || t.Name.EndsWith("Controller"));

        foreach (var controller in controllers)
        {
            sb.AppendLine($"Controller: {controller.Name}");
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var routeAttr = method.GetCustomAttributes().FirstOrDefault(a => a is HttpMethodAttribute) as HttpMethodAttribute;
                var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();

                sb.Append($"  Method: {method.Name}");

                if (routeAttr != null)
                    sb.Append($" [{routeAttr.HttpMethods.FirstOrDefault()}]");

                if (authorizeAttr != null)
                    sb.Append($" [Authorize{(string.IsNullOrEmpty(authorizeAttr.Policy) ? "" : $" Policy={authorizeAttr.Policy}")}]");

                sb.AppendLine();
                foreach (var param in method.GetParameters())
                {
                    sb.AppendLine($"    - {param.Name} : {param.ParameterType.Name}");
                }
            }
            sb.AppendLine();
        }
    }

    static void DocumentRazorPages(string pagesPath, StringBuilder sb)
    {
        sb.AppendLine("== Razor Pages ==");

        foreach (var file in Directory.GetFiles(pagesPath, "*.cshtml.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(pagesPath, file);
            sb.AppendLine($"Page: {relativePath.Replace("\\", "/")}");
            var lines = File.ReadAllLines(file);

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("public void On") || line.Trim().StartsWith("public async Task On"))
                {
                    sb.AppendLine($"  Handler: {line.Trim()}");
                }
            }

            sb.AppendLine();
        }
    }
}
