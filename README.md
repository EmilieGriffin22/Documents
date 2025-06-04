Why the [FromForm] parameters disappear in Swagger UI

In OpenAPI 3.x, query/header/path/cookie values are listed under the parameters section, but anything that comes from the HTTP body must be expressed with a single requestBody object.
[FromForm] tells ASP.NET Core to bind the values from a  multipart/form-data  or  application/x-www-form-urlencoded  request body – not from the query-string. Consequently, an operation that still lists those items as “parameters” is invalid and Swagger UI won’t show any input fields for them. Microsoft’s OpenAPI generator makes the same decision internally – “[FromForm] parameter(s) become a requestBody whose default content types are multipart/form-data or application/x-www-form-urlencoded”  ￼.

So the road-block you’re seeing isn’t Razor Pages; it’s that your custom document generator keeps the [FromForm] items in the wrong place.

⸻

A minimal, non-intrusive workaround
	1.	Let your generator keep discovering the handlers with EndpointDataSource/HandlerMethodDescriptor.
	2.	When you detect BindingSource.Form, move the item into operation.RequestBody and describe its schema.
	3.	Tell Swagger UI which media-type to use (application/x-www-form-urlencoded works for simple scalars; use multipart/form-data when files are involved).

Below is a trimmed OperationFilter that can be dropped into Swashbuckle (or the equivalent hook if you’re using Microsoft.AspNetCore.OpenApi). It removes the form parameters, builds a request body schema, and lets Swagger UI render the proper form:

public sealed class FormToRequestBodyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        var formParams = ctx.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<FromFormAttribute>() != null)
            .ToArray();
        if (!formParams.Any()) return;

        // 1. Remove them from the "parameters" array
        foreach (var p in formParams)
        {
            var existing = op.Parameters?.FirstOrDefault(x => x.Name == p.Name);
            if (existing != null) op.Parameters.Remove(existing);
        }

        // 2. Build a schema with one property per form field
        var props = formParams.ToDictionary(
            p => p.Name,
            p => ctx.SchemaGenerator.GenerateSchema(p.ParameterType, ctx.SchemaRepository));

        op.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["application/x-www-form-urlencoded"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = props
                    }
                }
            }
        };
    }
}

Register it once:

services.AddSwaggerGen(o => o.OperationFilter<FormToRequestBodyFilter>());

After that, Swagger UI will show regular text boxes (or file pickers for IFormFile) and send the request as a classic HTML form – exactly what your Razor Page handler expects.

A complete handler might look like:

public class ContactModel : PageModel
{
    // POST /Contact?handler=Save
    public IActionResult OnPostSave(
        [FromForm] string firstName,
        [FromForm] string lastName,
        [FromForm] IFormFile resume)
    {
        /* … */
    }
}

Your OpenAPI generator should emit:

POST /Contact
  parameters:
    - in: query
      name: handler      # Razor Pages still needs this!
      required: true
      schema: { type: string, enum: [Save] }
  requestBody:
    required: true
    content:
      multipart/form-data:
        schema:
          type: object
          properties:
            firstName: { type: string }
            lastName:  { type: string }
            resume:    { type: string, format: binary }

Swagger UI will now let you Try it out without any extra controller or “proxy” endpoint.

⸻

Three alternative approaches (pick the one that fits your project)

Approach	What you change	Pros	Cons
Add [Consumes("multipart/form-data")] or [Consumes("application/x-www-form-urlencoded")] on the handler	One attribute per handler	Zero custom code; the official generator already puts the parameter into requestBody when it sees the attribute	Still have to expose each handler through ApiExplorer with ApiExplorerSettings, and you may prefer not to touch the source files
Wrap the handler with a minimal-API endpointapp.MapPost("/Contact/Save", (ContactModel p, string firstName, …) => p.OnPostSave(firstName,…)).Accepts<ContactSaveDto>("multipart/form-data");	One line in Program.cs per handler	Works out-of-box with Swashbuckle/OpenAPI v9; no custom filters	Slightly more boilerplate; two routes hit the same code (Page and endpoint)
Switch the binding source to JSON ([FromBody] or a bound DTO)	Accept JSON instead of form data	Swagger UI already supports it; no custom filter	Front-end tests that depend on classic form posts must change


⸻

Tips & gotchas
	•	Remember the handler argument – Razor Pages picks the method based on OnPost{Handler}. Just add it as a required query parameter in the spec.
	•	If you need file upload support, use the same filter but emit multipart/form-data and mark file properties with format: binary.
	•	For large solutions, consider wrapping the above logic in a small NuGet-style “RazorPages.Swagger” helper so every project can enable it with a single line.

With one of the above patterns in place, you can exercise every Razor Page handler – including those that rely on classic form posts – straight from Swagger UI without introducing duplicate controller code.
