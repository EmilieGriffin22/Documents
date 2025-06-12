Below is a step-by-step recipe for getting Swagger UI to send your HTTP-only auth cookie and a per-request anti-forgery token, with minimal manual work beyond first logging in in your browser. We’ll do everything in .NET 8’s minimal-hosting style (if you still have a Startup.cs, just move the corresponding bits over).

---

## 1. In Program.cs: register & configure antiforgery

```csharp
var builder = WebApplication.CreateBuilder(args);

// … your existing Session / Authentication / Okta setup …

// 1️⃣ Configure antiforgery so that it issues a non‐HttpOnly cookie
builder.Services.AddAntiforgery(options =>
{
    // this is the header name we’ll send in JS
    options.HeaderName = "X-XSRF-TOKEN";
    // name the cookie and allow JS to read it
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

> **Why non‐HttpOnly?**
> We need the JS in Swagger to read the antiforgery cookie and then echo its value back in a header.

---

## 2. Expose a “get-token” endpoint

Still in Program.cs—*before* your `app.MapControllers()` / `app.MapRazorPages()`:

```csharp
var app = builder.Build();

app.UseStaticFiles();    // so our custom JS can be served
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// 2️⃣ New endpoint for Swagger UI to fetch the token
app.MapGet("/antiforgery/token", (HttpContext http, IAntiforgery anti) =>
{
    var tokens = anti.GetAndStoreTokens(http);
    return Results.Json(new { token = tokens.RequestToken });
});
```

This emits `{ "token": "..." }` on GET, and sets the antiforgery cookie.

---

## 3. Wire up Swashbuckle to inject a custom JS file

Still in Program.cs, where you call `app.UseSwaggerUI(...)`:

```csharp
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    // inject our fetch+interceptor logic
    c.InjectJavascript("/swagger-ui/swagger-custom.js");
});
```

Make sure `UseStaticFiles()` runs *before* this, and that you have a folder `wwwroot/swagger-ui/`.

---

## 4. Create wwwroot/swagger-ui/swagger-custom.js

```js
// wwwroot/swagger-ui/swagger-custom.js
(async function() {
  // fetch a fresh antiforgery token
  async function fetchCsrf() {
    const resp = await fetch('/antiforgery/token', { credentials: 'include' });
    if (!resp.ok) return null;
    const json = await resp.json();
    return json.token;
  }

  // initialize Swagger UI
  window.ui = SwaggerUIBundle({
    url: "/swagger/v1/swagger.json",
    dom_id: "#swagger-ui",
    presets: [
      SwaggerUIBundle.presets.apis,
      SwaggerUIStandalonePreset
    ],
    layout: "BaseLayout",

    // BEFORE every request…
    requestInterceptor: async (req) => {
      // 1) send your auth cookie
      req.credentials = 'include';

      // 2) fetch & attach antiforgery header
      const token = await fetchCsrf();
      if (token) {
        req.headers['X-XSRF-TOKEN'] = token;
      }
      return req;
    }
  });
})();
```

1. `req.credentials = 'include'` tells the browser to send same-origin cookies.
2. We pull down a fresh token from `/antiforgery/token` and stick it into our “X-XSRF-TOKEN” header.

---

## 5. (Optional) Annotate CSRF-protected endpoints in OpenAPI

If you’d like Swagger’s UI to *show* which operations require the antiforgery header, add a small `IOperationFilter`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

public class AddCsrfHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // look for [ValidateAntiForgeryToken]
        var hasAttr = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<ValidateAntiForgeryTokenAttribute>()
            .Any()
          || context.MethodInfo.DeclaringType
            .GetCustomAttributes(true)
            .OfType<ValidateAntiForgeryTokenAttribute>()
            .Any();

        if (!hasAttr) return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name        = "X-XSRF-TOKEN",
            In          = ParameterLocation.Header,
            Required    = true,
            Schema      = new OpenApiSchema { Type = "string" },
            Description = "Anti-forgery token"
        });
    }
}
```

Then register it in your `AddSwaggerGen`:

```csharp
builder.Services.AddSwaggerGen(c =>
{
    // … your existing filters …
    c.OperationFilter<AddCsrfHeaderOperationFilter>();
});
```

---

## 6. Final Program.cs sketch

```csharp
var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────
builder.Services
    // your existing Session / Cookie / Okta / AuthZ setup
    .AddSession(/* … */);

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName    = "X-XSRF-TOKEN";
    options.Cookie.Name   = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddSwaggerGen(c =>
{
    c.DocumentFilter<CustomRazorPageDocumentFilter>();
    c.DocumentFilter<CustomStaticPageDocumentFilter>();
    c.OperationFilter<CustomEndpointDocumentTransformer>();
    c.OperationFilter<AddCsrfHeaderOperationFilter>();
});

// ── Build & Middleware ───────────────────────────────────────────
var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// expose the antiforgery token
app.MapGet("/antiforgery/token", (HttpContext http, IAntiforgery anti) =>
{
    var tokens = anti.GetAndStoreTokens(http);
    return Results.Json(new { token = tokens.RequestToken });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    c.InjectJavascript("/swagger-ui/swagger-custom.js");
});

app.MapControllers();
app.MapRazorPages();

app.Run();
```

---

### Quick manual step

1. **First** log in through your app’s normal UI (so the auth cookie is issued).
2. Then browse to `/swagger`—Swagger UI will already have your cookie, will fetch the antiforgery token, and will include both on every request automatically.

That should give you a seamless Swagger UX with cookies + CSRF headers, plus OAS annotations for any `[ValidateAntiForgeryToken]` controllers.

===============================================================================

You don’t actually need to read the antiforgery cookie in JavaScript – the browser will send it automatically as long as you fetch with `credentials: 'include'`. Instead, have the server emit the token in the JSON payload of a dedicated endpoint, and leave *all* your cookies (auth + CSRF) as HTTP-only.

---

### 1) Keep your antiforgery cookie HTTP-only

In **Program.cs**, register antiforgery exactly as you normally would – **do not** override `Cookie.HttpOnly`. You only need to set the header name you’ll use:

```csharp
builder.Services.AddAntiforgery(options =>
{
    // this is the header JS will send back
    options.HeaderName = "X-XSRF-TOKEN";
    // (leave options.Cookie.* at their defaults, including HttpOnly = true)
});
```

---

### 2) Expose a “fetch token” endpoint

Still in **Program.cs**, before you map controllers/pages:

```csharp
app.MapGet("/antiforgery/token", (HttpContext http, IAntiforgery anti) =>
{
    // generates a new token & (re)sets the HTTP-only cookie
    var tokens = anti.GetAndStoreTokens(http);
    // return only the token string in JSON
    return Results.Json(new { token = tokens.RequestToken });
});
```

> Because the antiforgery cookie is HTTP-only, JS can’t read it – but it *will* be sent on this fetch, so the server is able to validate/generate the correct `RequestToken`.

---

### 3) Tell Swagger UI to include cookies & inject the token

In your `app.UseSwaggerUI(…)` call, inject a small JS file that:

* does `fetch('/antiforgery/token', { credentials: 'include' })`
* reads out `{ token: "…" }`
* sticks it into the `X-XSRF-TOKEN` header
* does `req.credentials = 'include'` so your auth cookie is sent on every request

```csharp
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    c.InjectJavascript("/swagger-ui/swagger-custom.js");
});
```

#### wwwroot/swagger-ui/swagger-custom.js

```js
(async function() {
  // helper to get our JSON token
  async function fetchCsrf() {
    const res = await fetch('/antiforgery/token', {
      method: 'GET',
      credentials: 'include'
    });
    if (!res.ok) return null;
    const { token } = await res.json();
    return token;
  }

  window.ui = SwaggerUIBundle({
    url: "/swagger/v1/swagger.json",
    dom_id: "#swagger-ui",
    presets: [
      SwaggerUIBundle.presets.apis,
      SwaggerUIStandalonePreset
    ],
    layout: "BaseLayout",
    requestInterceptor: async (req) => {
      // 1) send your auth cookie
      req.credentials = 'include';

      // 2) fetch & attach antiforgery header
      const token = await fetchCsrf();
      if (token) {
        req.headers['X-XSRF-TOKEN'] = token;
      }
      return req;
    }
  });
})();
```

---

### 4) (Optional) Mark CSRF-protected operations in OpenAPI

If you still want those endpoints flagged in Swagger:

```csharp
public class AddCsrfHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        var has = ctx.MethodInfo
            .GetCustomAttributes(true)
            .OfType<ValidateAntiForgeryTokenAttribute>()
            .Any();
        if (!has) return;

        op.Parameters ??= new List<OpenApiParameter>();
        op.Parameters.Add(new OpenApiParameter {
            Name        = "X-XSRF-TOKEN",
            In          = ParameterLocation.Header,
            Required    = true,
            Schema      = new OpenApiSchema { Type = "string" },
            Description = "Antiforgery token"
        });
    }
}

// … then in AddSwaggerGen:
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<AddCsrfHeaderOperationFilter>();
    // … your DocumentFilters for Razor pages, etc.
});
```

---

### 5) Final flow

1. **User logs in** normally → browser stores your HTTP-only auth cookie.
2. **Swagger UI loads** (JS served from `/swagger-ui/swagger-custom.js`).
3. Before each request, JS does

   ```js
   fetch('/antiforgery/token', { credentials: 'include' })
   ```

   the browser sends your HTTP-only CSRF cookie along, the server issues a fresh token in JSON, and JS grabs it.
4. JS then issues the real API call with

   * `req.credentials = 'include'` → sends auth+CSRF cookies
   * `X-XSRF-TOKEN: <token>` header → server’s antiforgery filter will validate it.

No need ever to turn off HttpOnly. Everything remains secure, automatic, and seamless.

======================================================================================

Here are three “no-endpoint” ways to get your antiforgery token into Swagger UI—each works without ever turning off HttpOnly on your cookies:

---

## A) Embed the token in a `<meta>` tag in your Razor layout

1. **In your `_Layout.cshtml`** (or whichever shared layout serves Swagger’s HTML) add:

   ```cshtml
   @inject Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery
   @{
     var tokens = antiforgery.GetAndStoreTokens(Context);
   }
   <head>
     <!-- … your existing head tags … -->
     <meta name="csrf-token" content="@tokens.RequestToken" />
   </head>
   ```

2. **In your `swagger-custom.js`** (injected via `InjectJavascript`) read it:

   ```js
   (function() {
     function getCsrfFromMeta() {
       const m = document.querySelector('meta[name="csrf-token"]');
       return m ? m.content : null;
     }

     window.ui = SwaggerUIBundle({
       /* … */
       requestInterceptor: (req) => {
         req.credentials = 'include';
         const token = getCsrfFromMeta();
         if (token) req.headers['X-XSRF-TOKEN'] = token;
         return req;
       }
     });
   })();
   ```

This way the token is written by Razor once when the page renders, and JS just reads the meta tag.

---

## B) Scrape it out of any existing Razor page’s hidden input

If you can’t touch the layout, but you have *any* Razor page that emits:

```html
<input 
  name="__RequestVerificationToken" 
  type="hidden" 
  value="…some-token…" />
```

you can fetch that page and parse it:

```js
(async function() {
  async function fetchCsrf() {
    const res = await fetch('/SomePageWithToken', { credentials: 'include' });
    if (!res.ok) return null;
    const html = await res.text();
    const match = html.match(/name="__RequestVerificationToken"\s+value="([^"]+)"/);
    return match ? match[1] : null;
  }

  window.ui = SwaggerUIBundle({
    /* … */
    requestInterceptor: async (req) => {
      req.credentials = 'include';
      const token = await fetchCsrf();
      if (token) req.headers['X-XSRF-TOKEN'] = token;
      return req;
    }
  });
})();
```

Here the browser still sends the HTTP-only antiforgery cookie on the fetch, the server regenerates the matching token in the hidden field, and your JS plucks it out of the HTML.

---

## C) Override the Swagger index as a Razor view

Rather than serving the stock `index.html`, you can:

1. **Copy** the Swashbuckle `index.html` into a Razor Page (e.g. `Pages/Swagger/Index.cshtml`).

2. Change its extension to `.cshtml` and at the top inject `IAntiforgery` exactly like in (A), emitting:

   ```cshtml
   <script>
     window.__CSRF_TOKEN = '@antiforgery.GetAndStoreTokens(Context).RequestToken';
   </script>
   ```

3. **Point Swashbuckle** at that instead of the default:

   ```csharp
   app.UseSwaggerUI(c =>
   {
     c.IndexStream = () =>
       File.OpenRead(Path.Combine(env.ContentRootPath,
                                  "Pages/Swagger/Index.cshtml"));
   });
   ```

4. In your custom JS:

   ```js
   requestInterceptor: (req) => {
     req.credentials = 'include';
     const token = window.__CSRF_TOKEN;
     if (token) req.headers['X-XSRF-TOKEN'] = token;
     return req;
   }
   ```

---

## D) (Most manual) Use Swagger’s “Authorize” dialog

If all else fails, you can:

1. Paste your antiforgery token into the Swagger **Authorize** modal as a custom header (`X-XSRF-TOKEN`).
2. Check “persist authorization” so it’s sent on every call.

—this doesn’t require any code changes, but obviously it’s manual each session.

---

All of these keep **all** your cookies (auth + antiforgery) as HTTP-only.  The browser will still send them whenever you do a fetch with

```js
req.credentials = 'include'
```

and your JS only needs a way to get the token string itself.

