Below is the pattern that normally works when you want Swagger UI to “talk to” a Razor-Page handler that relies on a custom model-binder (your QueryParameterBinder) and a List<QueryParameter> value:

⸻

1  Treat the complex binder parameter as body data, not a “normal” parameter
	•	Swagger-UI can only render rich editors (arrays, objects, etc.) when the data is described in the requestBody section of the OpenAPI document.
	•	Placing it in parameters (even with BindingSource.Custom) will make Swagger-UI fall back to primitive query/form-field editors, which cannot build a List<QueryParameter> for you.
	•	A Razor Page handler happily binds complex types from the body if you decorate it (or the PageModel) with [Consumes("application/json")] or [FromBody].

OpenAPI 3 body syntax you need (trimmed to essentials)  ￼

requestBody:
  required: true
  content:
    application/json:
      schema:
        type: object
        properties:
          query:
            type: string
          parameters:           # <-- your List<QueryParameter>
            type: array
            items:
              $ref: '#/components/schemas/QueryParameter'
          system:
            type: string
          dbId:
            type: string
components:
  schemas:
    QueryParameter:
      type: object
      properties:
        Name:   { type: string }
        Type:   { type: string }   # Int32, String, DateTime, …
        Value:  { type: string }
      required: [Name, Value]

MapClrTypeToOpenApiSchema can build the primitive parts automatically; complex parts you’ll hand-code once and $ref.

⸻

2  Update your setParameters helper

private void SetParameters(OpenApiOperation op, HandlerMethodDescriptor handler)
{
    var formProps = new Dictionary<string, OpenApiSchema>();
    foreach (var p in handler.Parameters)
    {
        var src = p.BindingInfo?.BindingSource;
        if (src == BindingSource.Form)          // unchanged
        {
            formProps[p.Name] = MapClrTypeToOpenApiType(p.ParameterType);
        }
        else if (src == BindingSource.Custom &&
                 typeof(IEnumerable<QueryParameter>).IsAssignableFrom(p.ParameterType))
        {
            // move to requestBody instead of Parameters
            EnsureJsonRequestBody(op);
            op.RequestBody.Content["application/json"].Schema
               .Properties.Add(p.Name, new OpenApiSchema {
                   Type = "array",
                   Items = new OpenApiSchema { Reference = new OpenApiReference {
                       Id = "QueryParameter", Type = ReferenceType.Schema } } });
        }
        else
        {
            op.Parameters.Add(new OpenApiParameter {
                In   = ParameterLocation.Query,
                Name = p.Name,
                Schema = MapClrTypeToOpenApiType(p.ParameterType)
            });
        }
    }

    // x-www-form-urlencoded section if you kept any [FromForm] items
    if (formProps.Any())
    {
        op.RequestBody = new OpenApiRequestBody {
            Required = true,
            Content = {
                ["application/x-www-form-urlencoded"] = new OpenApiMediaType {
                    Schema = new OpenApiSchema {
                        Type = "object",
                        Properties = formProps
                    }
                }
            }
        };
    }
}

EnsureJsonRequestBody is a tiny helper that adds an application/json section if it doesn’t exist yet.

⸻

3  What it looks like in Swagger UI
	•	You’ll get a single “schema” editor where the user pastes/edits JSON:

{
  "query": "SELECT * FROM Employees WHERE Id=@Id",
  "parameters": [
    { "name": "@Id", "type": "Int32", "value": "42" }
  ],
  "system": "GxP",
  "dbId": "Prod"
}

	•	Swagger-UI will send Content-Type: application/json and Razor Pages will hand the JSON string to QueryParameterBinder, which can deserialize or validate however it already does.

⸻

4  If you must stay with application/x-www-form-urlencoded

OpenAPI 3 technically supports objects/arrays in URL-encoded bodies, but the UX is poor because users must type bracketed keys or raw JSON strings. If you really need it:

requestBody:
  content:
    application/x-www-form-urlencoded:
      schema:
        type: object
        properties:
          parameters:
            type: string          # JSON-string inside the field
      encoding:
        parameters:
          contentType: application/json

Swagger-UI will still render a single text box for parameters; the caller pastes a JSON array. This is standards-compliant but rarely worth the friction  ￼ ￼.

⸻

5  Side notes & gotchas

Issue	Quick Fix
Razor Pages only allow one complex [FromBody] parameter.	Wrap everything in a DTO, or receive HttpRequest.Body and parse manually inside your binder.
Custom binder not triggered?	Be sure the binder is registered after AddControllers() / AddRazorPages() and that the action parameter keeps [ModelBinder].
Try-it-out shows 415/400 errors	Check the Consumes attribute on the PageModel; Swagger will mirror it automatically only if you add [Consumes] at method level.


⸻

Recap
	•	Put complex, custom-bound parameters in requestBody → Swagger-UI can serialize them.
	•	Use application/json unless your page absolutely requires form-url-encoded.
	•	Extend your setParameters logic to detect BindingSource.Custom and populate the body section instead of the parameters list.
	•	Document the structure once with a named schema (QueryParameter) so you avoid hand-rolled JSON in every operation.

With those tweaks Swagger UI will generate a clean editor for your Razor-Page handler, and you’ll keep your existing QueryParameterBinder untouched.



Great — since you’re already generating OpenApiOperation manually (not relying purely on reflection or ApiExplorer), here’s how you can adapt the logic from FormToRequestBodyFilter directly into your custom setParameters(OpenApiOperation operation, HandlerMethodDescriptor handler) method:

⸻

✅ What this method should do
	1.	Add parameters that are NOT [FromForm] to operation.Parameters.
	2.	Group all [FromForm] parameters into a synthetic requestBody with content type application/x-www-form-urlencoded.

⸻

✅ Modified setParameters implementation

private void setParameters(OpenApiOperation operation, HandlerMethodDescriptor handler)
{
    var nonFormParams = new List<OpenApiParameter>();
    var formParams = new Dictionary<string, OpenApiSchema>();

    foreach (var param in handler.Parameters)
    {
        var paramName = param.Name;
        var paramType = param.ParameterType;
        var bindingSource = param.BindingInfo?.BindingSource;

        if (bindingSource == BindingSource.Form)
        {
            // Treat this parameter as part of the requestBody
            formParams[paramName] = new OpenApiSchema
            {
                Type = mapClrTypeToOpenApiType(paramType),
            };
        }
        else
        {
            // Treat this parameter as a query/path/header parameter
            nonFormParams.Add(new OpenApiParameter
            {
                Name = paramName,
                In = ParameterLocation.Query, // Or use handler.RouteInfo to detect path parameters
                Required = !param.IsOptional,
                Schema = new OpenApiSchema
                {
                    Type = mapClrTypeToOpenApiType(paramType),
                }
            });
        }
    }

    // Attach non-form parameters
    operation.Parameters = nonFormParams;

    // Attach form parameters via requestBody
    if (formParams.Any())
    {
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["application/x-www-form-urlencoded"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = formParams
                    }
                }
            }
        };
    }
}


⸻

✅ Helper: mapClrTypeToOpenApiType

This is a basic utility to map .NET types to OpenAPI types:

private string mapClrTypeToOpenApiType(Type type)
{
    if (type == typeof(string)) return "string";
    if (type == typeof(int) || type == typeof(long)) return "integer";
    if (type == typeof(bool)) return "boolean";
    if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
    if (type == typeof(DateTime)) return "string"; // Use `format = "date-time"` if needed
    return "string"; // fallback
}


⸻

🧠 Notes
	•	You can further improve accuracy by checking param.ParameterType.IsAssignableTo(typeof(IFormFile)) and using multipart/form-data and "format": "binary" when needed.
	•	If you want to set enum, format, or validation constraints (e.g. min/maxLength), you’ll need a richer schema generator (e.g. call ctx.SchemaGenerator.GenerateSchema() like Swashbuckle does).
	•	If your Razor Pages use handler names, remember to inject the "handler" query parameter manually as well.

⸻

Let me know if you want to extend this with file uploads, nested types, or enum support — the core setup above gives you Swagger UI interactivity with [FromForm] just like a controller.




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
