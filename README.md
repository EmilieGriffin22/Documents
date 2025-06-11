Yes, you can make SwaggerUI send cookies and custom headers like RequestVerificationToken, especially since it’s served from the same origin as your authenticated application. Here’s how to make that work:

⸻

✅ 1. Enable Cookie Sending in SwaggerUI

SwaggerUI uses the Fetch API, and you need to explicitly enable credentials (cookies) in those requests.

In your SwaggerUI setup (typically in swagger-initializer.js or wherever you call SwaggerUIBundle()), set:

SwaggerUIBundle({
  url: "/swagger/v1/swagger.json",
  dom_id: "#swagger-ui",
  presets: [
    SwaggerUIBundle.presets.apis,
    SwaggerUIStandalonePreset
  ],
  requestInterceptor: (req) => {
    req.credentials = "include";  // <- THIS IS CRUCIAL FOR COOKIES
    return req;
  }
});

	•	req.credentials = "include" ensures that cookies (including auth and XSRF-TOKEN cookies) are sent with SwaggerUI’s fetch requests.

⸻

✅ 2. Set Custom Headers (e.g., RequestVerificationToken)

If you’re using an anti-forgery token that must be included in the header (common with ASP.NET apps), you can pull it from a cookie or a meta tag and inject it using the same requestInterceptor:

requestInterceptor: (req) => {
  req.credentials = "include";

  // Grab the token from a cookie (adjust name as needed)
  const token = getCookie("RequestVerificationToken");
  if (token) {
    req.headers["RequestVerificationToken"] = token;
  }

  return req;
}

Here’s a helper function to grab a cookie value:

function getCookie(name) {
  const match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
  if (match) return decodeURIComponent(match[2]);
  return null;
}


⸻

✅ 3. Verify Your Cookies Are Not HttpOnly

If your authentication or anti-forgery cookies are marked HttpOnly, JavaScript (including SwaggerUI) won’t be able to read them. This is by design. To work around this:
	•	Store a non-HttpOnly copy of the anti-forgery token in a readable cookie or meta tag.
	•	Authentication (like bearer tokens) can sometimes be passed via Authorization headers instead if cookies are locked down.

⸻

✅ 4. Optional: Inject Auth Headers via SwaggerUI Authorize Button

If your cookies aren’t an option, or you also use Authorization: Bearer xyz headers, you can configure the SwaggerUI securityDefinitions to allow users to enter tokens manually.

⸻

Summary

To fix your issue:
	•	Set req.credentials = "include" to enable cookies.
	•	Use requestInterceptor to inject any headers you need (like RequestVerificationToken).
	•	Ensure the required cookies aren’t HttpOnly if you’re reading them from JavaScript.

Let me know if you want help customizing this based on your exact anti-forgery setup or cookie names!


=====

In a .NET 8 application using builder.Services.AddSwaggerGen(), you configure the UI-related settings like adding cookies and headers not in AddSwaggerGen(), but in the Swagger UI middleware, which is typically added via app.UseSwaggerUI() in the Program.cs file.

Here’s how you do it step-by-step:

⸻

✅ 1. Configure AddSwaggerGen (optional, just for metadata)

This is where you configure your Swagger document generation (not UI behavior). For example:

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // Optional: Add security definition if using bearer tokens
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});


⸻

✅ 2. Configure UseSwaggerUI to Inject JavaScript for Cookies & Headers

You need to inject a custom JavaScript file to handle:
	•	Sending cookies (credentials: 'include')
	•	Reading and injecting RequestVerificationToken header from cookies

In Program.cs:

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");

    // Inject your custom JavaScript
    options.InjectJavascript("/swagger/custom-swagger.js");
});


⸻

✅ 3. Create /wwwroot/swagger/custom-swagger.js

Place this file in your project’s wwwroot/swagger/ folder:

window.onload = function () {
    const ui = SwaggerUIBundle({
        url: "/swagger/v1/swagger.json",
        dom_id: "#swagger-ui",
        presets: [
            SwaggerUIBundle.presets.apis,
            SwaggerUIStandalonePreset
        ],
        requestInterceptor: (req) => {
            req.credentials = "include"; // send cookies

            // Optional: Inject anti-forgery token from cookie
            const token = getCookie("RequestVerificationToken");
            if (token) {
                req.headers["RequestVerificationToken"] = token;
            }

            return req;
        }
    });

    function getCookie(name) {
        const match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
        return match ? decodeURIComponent(match[2]) : null;
    }
}


⸻

✅ 4. Ensure Static Files Are Served

In Program.cs (should be default):

app.UseStaticFiles();

This allows the Swagger UI to serve your custom-swagger.js.

⸻

🔐 Additional Notes
	•	If you’re using ASP.NET Core’s Anti-Forgery, and the token is stored in a RequestVerificationToken cookie, make sure it’s:
	•	Not HttpOnly
	•	Readable via JavaScript
	•	If you are using session/cookie-based authentication, also ensure SameSite is Lax or None and you’re serving over HTTPS if required.

⸻

Would you like help generating the anti-forgery cookie/token from your Razor Pages or MVC controller as well?

====
