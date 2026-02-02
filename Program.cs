using CareFleet.Models;
using CareFleet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<EmailService>();


// Add MVC
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    )
);

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
    }
    catch (Exception ex)
    {
        // Log error if database creation fails
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
    }
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// Auto-login middleware for Remember Me
app.Use(async (context, next) =>
{
    if (string.IsNullOrEmpty(context.Session.GetString("UserEmail")))
    {
        var authCookie = context.Request.Cookies["CareFleetAuth"];
        if (!string.IsNullOrEmpty(authCookie))
        {
            using (var scope = context.RequestServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == authCookie);
                if (user != null)
                {
                    context.Session.SetString("UserEmail", user.Email);
                    context.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                }
            }
        }
    }
    await next();
});

app.UseAuthorization();

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
