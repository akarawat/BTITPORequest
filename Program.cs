using BTITPORequest.Controllers;
using BTITPORequest.Data;
using BTITPORequest.Services;
using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Cookie Auth — LoginPath ชี้ไป SSO redirect
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath       = "/Auth/SsoRedirect";
        options.LogoutPath      = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan  = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name     = "BTITPORequest.Auth";
    });

// Session — ใช้ In-Memory (dev)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout         = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly     = true;
    options.Cookie.IsEssential  = true;
});

// HttpClient — BTDigitalSign API (5s connect timeout)
builder.Services.AddHttpClient("DigitalSign", client =>
{
    var baseUrl = builder.Configuration["DigitalSignApi:BaseUrl"] ?? "";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(8);
});

// DI Services
builder.Services.AddSingleton<DbContext>();
builder.Services.AddScoped<IDigitalSignService, DigitalSignService>(); // ต้องก่อน AuthService
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPOService, POService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<SendMailController>();  // Email notification service
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

// ── Routes ─────────────────────────────────────────────────
// Default MVC — controller/action ก่อน
app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");

app.Run();
