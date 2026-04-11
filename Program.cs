using BTITPORequest.Data;
using BTITPORequest.Services;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/SsoRedirect";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "BTITPORequest.Auth";
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// HttpClient — BTDigitalSign API
builder.Services.AddHttpClient("DigitalSign", client =>
{
    var baseUrl = builder.Configuration["DigitalSignApi:BaseUrl"] ?? "";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// DI
builder.Services.AddSingleton<DbContext>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPOService, POService>();
builder.Services.AddScoped<IDigitalSignService, DigitalSignService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// SSO callback at root "/"
app.MapControllerRoute("home-root", "", new { controller = "Home", action = "Index" });

// Default MVC route
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
