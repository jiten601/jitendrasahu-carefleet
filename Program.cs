using CareFleet.Models;
using CareFleet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CareFleet.Hubs;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ==================== AUTHENTICATION CONFIGURATION ====================

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "CareFleetAuth";
    options.DefaultChallengeScheme = "CareFleetAuth";
})
.AddCookie("CareFleetAuth", options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});


// Email service
builder.Services.AddTransient<EmailService>();
builder.Services.AddHttpContextAccessor();

// Background services
builder.Services.AddHostedService<AppointmentReminderService>();

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
    options.IdleTimeout = TimeSpan.FromMinutes(30);
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

    if (!path.StartsWith("/account/login") &&
        !path.StartsWith("/account/register") &&
        !path.StartsWith("/css") &&
        !path.StartsWith("/js") &&
        !path.StartsWith("/images"))
    {
        var userEmail = context.Session.GetString("UserEmail");
        var sessionCreatedAt = context.Session.GetString("SessionCreatedAt");

        if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(sessionCreatedAt))
        {
            if (DateTime.TryParse(sessionCreatedAt, out DateTime createdTime))
            {
                var sessionAge = DateTime.Now - createdTime;

                if (sessionAge.TotalMinutes > 30)
                {
                    context.Session.Clear();

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
                context.Session.SetString("UserEmail", user.Email);
                context.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                context.Session.SetString("UserRole", user.Role);
                context.Session.SetString("SessionCreatedAt", DateTime.Now.ToString("o"));
            }
            else
            {
                context.Response.Cookies.Delete("CareFleetAuth");
            }
        }
    }

    await next();
});

// -------------------- AUTHENTICATION ENFORCEMENT --------------------
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";

    // Public paths that do not require authentication
    var publicPaths = new[] {
        "/",
        "/home",
        "/home/index",
        "/home/privacy",
        "/home/about",
        "/home/services",
        "/home/error",
        "/account/login",
        "/account/register",
        "/account/verifyotp",
        "/account/forgotpassword",
        "/account/resetpassword",
        "/account/changepassword"
    };

    // Check if current path is public
    bool isPublic = publicPaths.Any(p => path == p || path.StartsWith(p + "/"));

    // Skip static files (extra safety, though UseStaticFiles usually handles these)
    bool isStatic = path.StartsWith("/css") || 
                    path.StartsWith("/js") || 
                    path.StartsWith("/images") || 
                    path.StartsWith("/lib") ||
                    path.EndsWith(".css") ||
                    path.EndsWith(".js") ||
                    path.Contains(".styles.css");

    if (!isPublic && !isStatic)
    {
        var userEmail = context.Session.GetString("UserEmail");
        if (string.IsNullOrEmpty(userEmail))
        {
            // Set LastVisitedPath so they can be redirected back after login
            var returnUrl = context.Request.Path + context.Request.QueryString;
            
            // Avoid setting login/register paths as return URL
            if (!path.Contains("/account/login") && !path.Contains("/account/register"))
            {
                context.Response.Cookies.Append("LastVisitedPath", returnUrl);
            }
            
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    await next();
});

// Authentication
app.UseAuthentication();

// Authorization
app.UseAuthorization();

// -------------------- ROUTES --------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapHub<VideoCallHub>("/videoCallHub");

app.Run();