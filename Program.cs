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

// Ensure database is created and up to date
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        // Check if database can connect
        if (context.Database.CanConnect())
        {
            // Try to check if Doctors table exists
            try
            {
                // Try to query the Doctors table - if it fails, table doesn't exist
                var testQuery = context.Doctors.Count();
                logger.LogInformation("Database tables are up to date.");
            }
            catch
            {
                // Tables are missing, recreate database (development only)
                // WARNING: This will delete all existing data in development mode
                if (app.Environment.IsDevelopment())
                {
                    logger.LogWarning("Missing tables detected. Recreating database (this will delete all existing data)...");
                    try
                    {
                        context.Database.EnsureDeleted();
                        context.Database.EnsureCreated();
                        logger.LogInformation("Database recreated successfully with all tables.");
                    }
                    catch (Exception deleteEx)
                    {
                        logger.LogError(deleteEx, "Failed to recreate database. Please ensure no connections are active and try again.");
                    }
                }
                else
                {
                    logger.LogError("Database tables are missing. Please run migrations in production.");
                }
            }
        }
        else
        {
            // Database doesn't exist, create it
            context.Database.EnsureCreated();
            logger.LogInformation("Database created successfully.");
        }
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

app.UseAuthorization();

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
