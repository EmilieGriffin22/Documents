# Documents

Given your goals and constraints—particularly the need to document both **ASP.NET Core Razor Pages** and **Controller-based APIs** for SwaggerUI consumption—here’s a breakdown of **modern .NET-compatible approaches** that balance automation with minimal disruption to existing code.

---

## ✅ **1. Understand the Limitation: Why Razor Pages Aren’t Documented by Default**

Swashbuckle, NSwag, and similar OpenAPI generators work out-of-the-box with:

* `ControllerBase`-derived APIs
* `[ApiController]` attributes
* Minimal APIs (with `MapGet`, etc.)

Razor Pages, by design, aren’t seen as HTTP APIs—they’re page endpoints that execute handlers (`OnGet`, `OnPost`, etc.), often binding from the route, query, or form. They’re not decorated with OpenAPI-compatible metadata.

---

## ✅ **2. Viable Options and Recommended Strategies**

### 🛠 Option A: **Custom Swagger Document Filter + Reflection on Razor Pages**

You mentioned starting with `EndpointDataSource`. That’s a good start but limited. Here's how to go further:

* Use `IEndpointRouteBuilder.DataSources` to extract `Endpoint` metadata.
* Use `RazorPagesEndpointDataSource` (or filter endpoints with route pattern `.cshtml`).
* Use **reflection** on the `PageModel` classes to:

  * Enumerate `OnGet`, `OnPost`, `OnPut`, etc.
  * Extract `[BindProperty]`, `[FromQuery]`, etc. annotations
  * Construct corresponding `OpenApiOperation` and `OpenApiParameter` objects.

Then use a custom:

```csharp
public class RazorPageDocumentFilter : IDocumentFilter
```

to inject synthetic `OpenApiPathItem` entries into the `SwaggerDoc`.

#### ✅ Pros:

* Works with Swashbuckle.
* Respects current code with no intrusive edits.
* Semi-automated, if well-scaffolded.

#### ❗Cons:

* You must define your own mapping from Razor metadata to OpenAPI.
* Requires maintenance when new pages or handlers are added.

---

### 🧩 Option B: **Use NSwagStudio or NSwag.MSBuild for Manual Augmentation**

Use [NSwag](https://github.com/RicoSuter/NSwag) tools to *manually define* documentation for your Razor Pages using YAML/JSON.

You can:

* Define OpenAPI definitions for Razor Pages endpoints in a `.json` file.
* Merge this with automatically generated Swashbuckle/NSwag output via **NSwag CLI/MSBuild**.

> 📦 NuGet: `NSwag.MSBuild`, `NSwag.AspNetCore`

#### ✅ Pros:

* Fully Swagger-compliant.
* Can live alongside auto-generated controller docs.
* Clean separation of manual vs. automatic.

#### ❗Cons:

* Tedious without tooling.
* Not fully DRY unless paired with T4 templates or reflection scaffolds.

---

### 🔌 Option C: **Create Custom Middleware That Emits OpenAPI Descriptions**

If you want Swagger to treat Razor Pages like API endpoints, you can write a small middleware or source generator to:

* Introspect the loaded Razor Page assemblies at startup
* Emit a `swagger-extra.json` from metadata
* Feed that into SwaggerUI using `options.SwaggerEndpoint()`.

You might even:

* Use `Microsoft.AspNetCore.Mvc.ApplicationParts` to enumerate Razor Pages
* Combine with Roslyn-based source parsing to extract docs and parameters

#### 🛠 Tools to Support This:

* `Swashbuckle.AspNetCore`
* `Microsoft.AspNetCore.Mvc.RazorPages`
* `Microsoft.OpenApi.Models`
* [RazorPageRouteModelProvider](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.RazorPages/src/Infrastructure/RazorPagesOptions.cs) (advanced)

---

## 🚧 Optional: Architectural Enhancements

### 🧱 Pattern: **Treat Razor Page Logic as Services**

If OnGet/OnPost logic is dense, you could:

* Move logic into injectable service classes
* Create thin controller wrappers for pages (e.g., `/api/files/list` that wraps the Razor Page logic)
* Auto-document these minimal APIs via Swashbuckle.

This would enable OpenAPI documentation with no Razor parsing required.

---

## 🔚 Summary: Recommendations

| Goal                            | Recommended Technique                                  |
| ------------------------------- | ------------------------------------------------------ |
| **Minimal intrusion**           | `IDocumentFilter` + Reflection on Razor Pages          |
| **Swagger-compliant + precise** | NSwag YAML overlay or NSwag.MSBuild                    |
| **Automated + DRY but complex** | Middleware + Roslyn/Reflection page inspection         |
| **Scalable + modern**           | Wrap business logic into services + minimal API façade |

---

## 🔧 Suggested Packages

* [`Swashbuckle.AspNetCore`](https://www.nuget.org/packages/Swashbuckle.AspNetCore)
* [`NSwag.AspNetCore`](https://www.nuget.org/packages/NSwag.AspNetCore)
* [`Microsoft.OpenApi`](https://www.nuget.org/packages/Microsoft.OpenApi)
* [`Microsoft.AspNetCore.Mvc.RazorPages`](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.RazorPages)

Response #2 

Below is a “menu” of .NET-8-friendly options, ordered from **least intrusive** to **most DIY**.
Pick the level of automation that best matches your risk tolerance and the amount of code you can safely touch.

---

### 1  Plug-and-play: **Weikio ApiFramework + NSwag**

| Why it helps                                                                                                                                     | How to wire it in |
| ------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------- |
| *Wraps* controllers, Minimal-API routes **and Razor-Page handlers** into a single OpenAPI document. No need to re-organise your UI layer.        | \`\`\`bash        |
| dotnet add package Weikio.ApiFramework.AspNetCore                                                                                                |                   |
| dotnet add package Weikio.ApiFramework.AspNetCore.NSwag   # SwaggerUI/NSwag UI                                                                   |                   |
| ` `csharp                                                                                                                                        |                   |
| builder.Services.AddApiFramework();        // in Program.cs                                                                                      |                   |
| app.UseApiFramework();                     // before app.Run()                                                                                   |                   |
| \`\`\` ApiFramework walks `EndpointDataSource`, discovers `PageActionDescriptor`s and emits Swagger/Redoc endpoints out-of-the-box. ([NuGet][1]) |                   |

**Pros:** zero refactoring, Swagger UI ready in minutes.
**Cons:** extra dependency (15 KB IL + NSwag), opinionated conventions you may need to tweak.

---

### 2  “Thin façade” pattern: **Minimal-API wrappers**

If you’d rather stay inside Microsoft-owned stacks:

```csharp
// Program.cs  (no changes inside *.cshtml.cs files)
app.MapGet("/files/{path}", (FileExplorerPage page, string path)
        => page.OnGetAsync(path))
   .WithName("BrowseFiles")
   .WithOpenApi();                 // Microsoft.AspNetCore.OpenApi
```

* `WithOpenApi()` (in the **Microsoft.AspNetCore.OpenApi** package) injects
  the metadata that **AddEndpointsApiExplorer()** and **Swashbuckle** need. ([Microsoft Learn][2], [Microsoft Learn][3])
* You keep Razor Pages as the view layer—only a thin adapter method is added in *Program.cs* (or an extension method in a separate file), so other devs’ code stays untouched.

**Pros:** first-party libraries only, perfect Swagger accuracy.
**Cons:** you still write one wrapper per page handler you want exposed.

---

### 3  “Tell Swagger the truth”: **Custom `IApiDescriptionProvider`**

When you really need full automation *and* no extra NuGet:

1. Create a class that implements [`IApiDescriptionProvider`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.apiexplorer.iapidescriptionprovider).
2. In `OnProvidersExecuting` inspect `context.Results`, find entries whose `ActionDescriptor` is a `PageActionDescriptor`, and translate each handler (`OnGet*`, `OnPost*`…) plus its `ParameterDescriptor`s into a new `ApiDescription`.
3. Register the provider:

   ```csharp
   services.AddTransient<IApiDescriptionProvider, RazorPageApiDescriptionProvider>();
   ```
4. Opt-in per page with

   ```csharp
   @attribute [ApiExplorerSettings(IgnoreApi = false)]
   ```

   or globally by convention.

Because Swashbuckle and NSwag just read `IApiDescriptionGroupCollectionProvider`, no further code is required. ([Andrew Lock | .NET Escapades][4], [Stack Overflow][5])

**Pros:** no third-party packages; everything updates automatically.
**Cons:** ≈200 lines of reflection/metadata code you must own and test.

---

### 4  Half-manual fallback: **NSwag CLI or Swashbuckle CLI**

If Razor-Page endpoints are few and change rarely, generate the OpenAPI file via CLI for the controller/minimal side only, then **merge small YAML fragments** that describe each page handler:

```bash
nswag aspnetcore2openapi /assembly:MyApp.dll /output:openapi.tmp.json
openapi-merge openapi.tmp.json PagesSpec/*.yaml > openapi.json
```

Automate the merge in a `dotnet msbuild` *Target* or GitHub Action.

---

## Recommended package stack (modern .NET 8)

| Concern                                   | Package                                                                  |
| ----------------------------------------- | ------------------------------------------------------------------------ |
| Base Swagger/OpenAPI                      | **Swashbuckle.AspNetCore** (≥ 6.6) ([Microsoft Learn][6])                |
| Built-in OpenAPI helpers for Minimal APIs | **Microsoft.AspNetCore.OpenApi** (ships with SDK) ([Microsoft Learn][2]) |
| Versioned docs                            | **Asp.Versioning.ApiExplorer** (optional)                                |
| Third-party full-stack auto-discovery     | **Weikio.ApiFramework.AspNetCore** + `.NSwag` ([NuGet][1])               |

---

## Architectural guidance

* **Separate concerns** Keep business logic in services that both Razor Pages and APIs call. The wrapper/minimal approach (option 2) enforces this naturally.
* **Opt-in visibility** Use `[ApiExplorerSettings]` (or page conventions) so that *only the endpoints you truly want public* appear in Swagger.
* **Document non-trivial handlers** Add `SwaggerOperation`, `ProducesResponseType`, and XML comments for parameters that aren’t obvious—Swagger will surface them automatically in UI.

---

### TL;DR

| Need                                           | Best fit                                          |
| ---------------------------------------------- | ------------------------------------------------- |
| *“I just need Swagger tonight.”*               | **ApiFramework** (option 1)                       |
| *“I can add a few lines in Program.cs.”*       | Minimal-API wrappers + `WithOpenApi()` (option 2) |
| *“I want it 100 % automatic and first-party.”* | Custom `IApiDescriptionProvider` (option 3)       |
| *“Docs change once a quarter.”*                | Hand-merge YAML fragments (option 4)              |

Start with option 2—it’s only a handful of one-liner wrappers and requires zero changes to team-mates’ Razor code. If the project later outgrows that, graduate to ApiFramework or your own provider without losing any work.

[1]: https://www.nuget.org/packages/weikio.apiframework.aspnetcore?utm_source=chatgpt.com "Weikio.ApiFramework.AspNetCore 1.2.1 - NuGet"
[2]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-9.0&utm_source=chatgpt.com "Overview of OpenAPI support in ASP.NET Core API apps"
[3]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&utm_source=chatgpt.com "Generate OpenAPI documents - Learn Microsoft"
[4]: https://andrewlock.net/introduction-to-the-apiexplorer-in-asp-net-core/?utm_source=chatgpt.com "Introduction to the ApiExplorer in ASP.NET Core"
[5]: https://stackoverflow.com/questions/57559378/what-does-the-attribute-apiexplorersettingsignoreapi-true-do?utm_source=chatgpt.com "What does the attribute \"[ApiExplorerSettings(IgnoreApi = true)]\" do?"
[6]: https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger?view=aspnetcore-8.0&utm_source=chatgpt.com "ASP.NET Core web API documentation with Swagger / OpenAPI"
