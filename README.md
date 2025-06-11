To configure **SwaggerUI** in your .NET application so that it **includes the correct cookies and antiforgery/security headers** for requests to both **API endpoints** and **Razor Pages**, you need to handle a few key areas:

---

### ✅ Step-by-Step Configuration

#### 1. **Enable Cookie Authentication in Swagger**
You are using cookie-based auth (`AddCookie`), so Swagger needs to send cookies in requests.

**SwaggerUI doesn’t send cookies by default** unless the browser allows it, and the backend is set to accept it.

To allow credentials:
```csharp
app.UseSwaggerUI(c =>
{
    c.ConfigObject.AdditionalItems["withCredentials"] = true; // Enables sending cookies
});
```

---

#### 2. **Add Security Scheme for Antiforgery Token**
If your Razor Pages require an antiforgery header (e.g., `RequestVerificationToken`), you need to:

- **Expose the token** in the Swagger UI (can be via JS injection)
- **Automatically attach it to requests**

Antiforgery token is typically in a cookie like `__RequestVerificationToken`, and you need to copy it into a header like `RequestVerificationToken`.

Unfortunately, **SwaggerUI doesn't natively support injecting headers from cookies**, so you’ll need to inject JavaScript in Swagger UI.

```csharp
app.UseSwaggerUI(c =>
{
    c.HeadContent = @"
        <script>
        window.ui.getConfigs().then(config => {
            const requestInterceptor = (req) => {
                const token = document.cookie.match(/__RequestVerificationToken=([^;]+)/);
                if (token && token.length > 1) {
                    req.headers['RequestVerificationToken'] = decodeURIComponent(token[1]);
                }
                return req;
            };
            window.ui.getConfigs().then(config => {
                config.requestInterceptor = requestInterceptor;
            });
        });
        </script>";
});
```

Alternatively, host a custom version of Swagger UI that has this script pre-integrated.

---

#### 3. **Ensure CORS is Not Blocking Cookies**
Ensure your backend allows cookies in cross-origin requests (if applicable):

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("https://your-frontend.com")
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});
```

And in middleware:

```csharp
app.UseCors();
```

---

#### 4. **Session and Antiforgery Integration**
You already have `AddSession()` and `AddAntiforgery()` configured. Just ensure:

- The antiforgery token is available in responses (e.g., rendered in Razor pages or exposed via an API call)
- Swagger calls (to both APIs and Razor pages) include that token via the JS mentioned above

---

### ✅ Summary

To make SwaggerUI work with your existing cookie + antiforgery + Okta auth setup:

1. Enable `withCredentials` in SwaggerUI config.
2. Use a JavaScript interceptor to extract the antiforgery cookie and set it as a header.
3. Confirm CORS allows credentials.
4. Make sure any endpoints Swagger is hitting expect the token via header and not form/body.

Let me know if you want a ready-made JS interceptor script to inject or help hosting a customized SwaggerUI page.