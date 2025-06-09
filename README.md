Below is one practical way to refactor your OpenAPI generation so that every Razor-Page request is sent to

/page?handler={handlerName}&...

instead of

/page/{handlerName}

The idea is:
	•	Only one path item per Razor Page – e.g. /Reports or /Reports/{id}.
	•	A single query parameter called handler enumerates the available page-handlers (OnGet, OnPostSave, etc.).
	•	Per handler-specific parameters are still listed, but they are flagged in the description so Swagger-UI users know which handler they belong to.

⸻

1 · Collect handlers page-by-page

var grouped = allHandlers          // IEnumerable<HandlerMethodDescriptor>
              .GroupBy(h => h.PageRoute);   //  "/Reports"  "/Reports/{id}" ...

2 · Create—or reuse—the path item

if (!doc.Paths.TryGetValue(pageRoute, out var pathItem))
    doc.Paths[pageRoute] = pathItem = new OpenApiPathItem();

3 · Build one OpenAPI operation per HTTP verb

The outer loop is per HTTP verb (GET, POST…).
Inside it, collect every handler that uses that verb.

foreach (var verbGroup in handlersByVerb)
{
    // This is the common operation for GET or POST, etc.
    var op = new OpenApiOperation
    {
        OperationId = $"{verbGroup.Key}_{TrimSlashes(pageRoute)}",
        Tags        = new List<OpenApiTag> { new() { Name = Path.GetFileName(pageRoute) } },
        Parameters  = new List<OpenApiParameter>(),
        Responses   = BuildSuccessAndErrorResponses(verbGroup)
    };

    /* --- add the shared "handler" query parameter --- */
    op.Parameters.Add(new OpenApiParameter
    {
        Name        = "handler",
        In          = ParameterLocation.Query,
        Required    = true,
        Description = "Razor-Page handler to invoke",
        Schema      = new OpenApiSchema
        {
            Type = "string",
            Enum = verbGroup.Select(h => new OpenApiString(h.HandlerName))
                            .Cast<IOpenApiAny>()
                            .ToList()
        }
    });

    /* --- add the parameters that belong to each handler --- */
    foreach (var h in verbGroup)
    {
        foreach (var p in h.Parameters)            // your existing reflection code
        {
            var apiParam = ToOpenApiParameter(p);  // your existing helper
            apiParam.Description =
                $"(handler = {h.HandlerName}) " + apiParam.Description;
            apiParam.Required = false;            // otherwise they’d all be required
            op.Parameters.Add(apiParam);
        }
    }

    /* --- attach the operation to the path --- */
    pathItem.AddOperation(verbGroup.Key.ToOpenApiOperationType(), op);
}

Why mark handler-specific parameters as optional?
OpenAPI permits only one operation per path + verb, so every parameter must be satisfiable for every value of handler.
Swagger-UI will still show them; the text “(handler = Save)” tells users which ones actually apply.

⸻

4 · Remove the old /page/{handler} paths

If you generated them earlier, simply skip them now:

// when you enumerate HandlerMethodDescriptor instances
// DO NOT call AddPath for the /page/{handlerName} route


⸻

5 · Result in Swagger-UI
	•	/Reports ▶ GET
	•	handler (enum drop-down: default, Details)
	•	id (only for handler = Details)
	•	/Reports ▶ POST
	•	handler (Save, Delete)
	•	file (handler = Save)
	•	reason (handler = Delete)

Users pick a value from the handler drop-down and Swagger-UI sends
/Reports?handler=Save or /Reports?handler=Delete with the right body or form-data.

⸻

6 · Helper utilities used above

static OperationType ToOpenApiOperationType(this string httpVerb) =>
    Enum.Parse<OperationType>(httpVerb, ignoreCase: true);

static string TrimSlashes(string s) => s.Trim('/');

static OpenApiResponses BuildSuccessAndErrorResponses(
    IEnumerable<HandlerMethodDescriptor> handlers)
{
    // your own logic – often a plain 200 and 400 is enough
}


⸻

Caveats & extensions
	•	If you need completely separate request bodies per handler, you can use the
OpenAPI 3.0 requestBody.content["application/json"].schema.oneOf trick,
keyed by handler.
The simple parameter-union above is usually “good enough” for test tooling.
	•	Don’t forget to regenerate your SwaggerDoc after these changes so the UI picks them up.

That’s all you need—your handlers will now appear as a single page endpoint with a handler query parameter, while every other parameter continues to work exactly as before.