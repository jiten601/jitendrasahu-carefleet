using CareFleet.Models;
using CareFleet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Email service
builder.Services.AddTransient<EmailService>();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    )
);

// Session (note: 30 minutes)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // session expires after 30 mins
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// -------------------- APP --------------------

var app = builder.Build();

// Ensure database connection
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        services.GetRequiredService<ApplicationDbContext>();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database connection failed.");
    }
}

// -------------------- MIDDLEWARE --------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session
app.UseSession();

// -------------------- SESSION TIMEOUT ENFORCEMENT --------------------
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    
    // Skip session timeout check for login/register pages and static files
    if (!path.StartsWith("/account/login") && 
        !path.StartsWith("/account/register") && 
        !path.StartsWith("/css") && 
        !path.StartsWith("/js") && 
        !path.StartsWith("/images"))
    {
        var userEmail = context.Session.GetString("UserEmail");
        var sessionCreatedAt = context.Session.GetString("SessionCreatedAt");

        // If user is logged in, check session age
        if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(sessionCreatedAt))
        {
            if (DateTime.TryParse(sessionCreatedAt, out DateTime createdTime))
            {
                var sessionAge = DateTime.Now - createdTime;

                // If session is older than 30 minutes, clear it and redirect
                if (sessionAge.TotalMinutes > 30)
                {
                    context.Session.Clear();
                    context.Response.Cookies.Delete("CareFleetAuth"); // Prevent auto-login from restoring session
                    context.Response.Redirect("/Account/Login");
                    return;
                }
            }
        }
    }

    await next();
});

// -------------------- AUTO LOGIN (REMEMBER ME) --------------------
app.Use(async (context, next) =>
{
    // If session is empty
    if (string.IsNullOrEmpty(context.Session.GetString("UserEmail")))
    {
        var authCookie = context.Request.Cookies["CareFleetAuth"];

        if (!string.IsNullOrEmpty(authCookie))
        {
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == authCookie);

            if (user != null)
            {
                // Restore session with timestamp
                context.Session.SetString("UserEmail", user.Email);
                context.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                context.Session.SetString("UserRole", user.Role);
                context.Session.SetString("SessionCreatedAt", DateTime.Now.ToString("o"));
            }
            else
            {
                // Invalid cookie
                context.Response.Cookies.Delete("CareFleetAuth");
            }
        }
    }

    await next();
});

// Authorization
app.UseAuthorization();

// -------------------- ROUTES --------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();
