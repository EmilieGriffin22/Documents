Here’s the quickest way to solve both issues without changing the rest of your filter logic.

⸻

1  Expose both application/json and application/x-www-form-urlencoded

Because OpenAPI lets you list several media-types for the same request body, simply add them side-by-side in the Content map:

// formProps = Dictionary<string, OpenApiSchema> you already built
if (formProps.Any())
{
    var schema = new OpenApiSchema
    {
        Type       = "object",
        Properties = formProps,
        Required   = new HashSet<string>(formProps.Keys)
    };

    operation.RequestBody = new OpenApiRequestBody
    {
        Required = true,
        Content  = new Dictionary<string, OpenApiMediaType>
        {
            ["application/x-www-form-urlencoded"] = new OpenApiMediaType { Schema = schema },
            ["application/json"]                  = new OpenApiMediaType { Schema = schema }
            // add ["multipart/form-data"] if you also want file upload
        }
    };
}

Swagger UI will now show a little drop-down beside “Try it out” so the caller can pick either encoding. This is exactly the pattern the Swashbuckle examples (and many blog posts) use to describe a form-body alongside JSON ￼.

⸻

2  Generate a schema for List<QueryParameter> (or any generic list)

The crash happens because the helper that maps CLR → OpenAPI types doesn’t recognise generic collections. Swap in the three helpers below; they treat anything that implements IEnumerable (except string) as an array, recurse on the element type, and otherwise fall back to primitives or a POCO walk:

private static bool IsEnumerable(Type t) =>
    typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string);

// 1  Primitive map ---------------------------------------------------
private string MapClrTypeToOpenApiType(Type type)
{
    type = Nullable.GetUnderlyingType(type) ?? type;

    if (type == typeof(string))                     return "string";
    if (type == typeof(bool))                       return "boolean";
    if (type.IsPrimitive || type.IsEnum)            return "integer";
    if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                                                    return "number";
    if (IsEnumerable(type))                         return "array";
    return "object";
}

// 2  Full schema generator ------------------------------------------
private OpenApiSchema GenerateSchema(Type type)
{
    if (IsEnumerable(type))
    {
        Type elem = type.IsArray
                  ? type.GetElementType()
                  : (type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object));

        return new OpenApiSchema
        {
            Type  = "array",
            Items = GenerateSchema(elem)
        };
    }

    var simple = MapClrTypeToOpenApiType(type);
    if (simple != "object")
        return new OpenApiSchema { Type = simple };

    var obj = new OpenApiSchema
    {
        Type       = "object",
        Properties = new Dictionary<string, OpenApiSchema>()
    };

    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        obj.Properties[p.Name] = GenerateSchema(p.PropertyType);

    return obj;
}

When the parameter is List<QueryParameter> the code above first recognises that it’s an enumerable, pulls out the element type (QueryParameter), and generates:

type: array
items:
  type: object      # schema from QueryParameter’s public properties
  properties:
    Name:  { type: string }
    Value: { type: string }
    Type:  { type: string }

QueryParameter is just a plain DevExpress POCO that exposes those fields, so Swagger can serialise a JSON array that your binder happily deserialises into a real List<QueryParameter> ￼.

Note – If you prefer reusable component definitions, stash each complex schema in context.SchemaRepository and return a $ref instead of the inline object. The detection logic above still applies.

⸻

3  No more per-property helper

Now everything funnels through GenerateSchema, so you can delete any separate GetPropertySchema helper (or reduce it to a single => GenerateSchema(t); line).

⸻

Result
	•	Users get a toggle between JSON and form-url-encoded bodies.
	•	Generic list parameters (including List<QueryParameter>) appear correctly in the UI and no longer throw at runtime.

Compile, refresh Swagger UI, and the endpoint should be fully interactive.
