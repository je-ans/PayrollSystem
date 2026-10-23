using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Interfaces;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;


var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONTROLLERS CONFIGURATION
// ============================================================
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter("Permission"));
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ============================================================
// DATABASE CONFIGURATION - OPTIMIZED
// ============================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlServerOptions =>
        {
            //// Enable retry logic for transient failures
            //sqlServerOptions.EnableRetryOnFailure(
            //    maxRetryCount: 3,
            //    maxRetryDelay: TimeSpan.FromSeconds(5),
            //    errorNumbersToAdd: null);

            // Command timeout
            sqlServerOptions.CommandTimeout(30);

            // Performance: Use split queries for better performance with includes
            sqlServerOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

    // EF Core Performance Optimizations
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Default to no-tracking
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment()); // Only in dev
    options.EnableDetailedErrors(builder.Environment.IsDevelopment()); // Only in dev

    // OPTIONAL: Enable compiled model for production (run: dotnet ef dbcontext optimize)
    // if (!builder.Environment.IsDevelopment())
    // {
    //     options.UseModel(CompiledModels.ApplicationDbContextModel.Instance);
    // }
});

// ============================================================
// IDENTITY CONFIGURATION
// ============================================================
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Sign-in settings
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddRoles<ApplicationRole>();

// ============================================================
// MEMORY CACHE - OPTIMIZED
// ============================================================
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Limit cache size (1024 items)
    options.CompactionPercentage = 0.25; // Compact 25% when limit reached
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

// ============================================================
// RESPONSE CACHING (Optional - for static content)
// ============================================================
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 1024 * 1024; // 1MB
    options.UseCaseSensitivePaths = false;
});

// ============================================================
// RESPONSE COMPRESSION (Optional - reduces bandwidth)
// ============================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ============================================================
// CORE SERVICES
// ============================================================
builder.Services.AddScoped<AuditHelper>();
builder.Services.AddLogging();
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// ============================================================
// CUSTOM OPTIMIZED SERVICES (NEW)
// ============================================================
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();
builder.Services.AddScoped<IOptimizedUserService, OptimizedUserService>();
builder.Services.AddScoped<IStartupOptimizationService, StartupOptimizationService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IPayrollComputationService, PayrollComputationService>();
builder.Services.AddScoped<IAllowanceService, AllowanceService>();
builder.Services.AddScoped<IDeductionService, DeductionService>();
builder.Services.AddScoped<BiometricDataParser>();
builder.Services.AddScoped<TimeEntryProcessingService>();
builder.Services.AddScoped<DtrAttendanceCalculationService>();
builder.Services.AddScoped<IAttendanceComputationService, AttendanceComputationService>();
builder.Services.AddScoped<IWorkScheduleService, WorkScheduleService>();
builder.Services.AddScoped<IScheduleChangeRequestService, ScheduleChangeRequestService>();
builder.Services.AddScoped<IScheduleTransactionConflictService, ScheduleTransactionConflictService>();
builder.Services.AddScoped<IPayrollTimeCardFromDtrService, PayrollTimeCardFromDtrService>();
builder.Services.AddScoped<ITimeLogJustificationService, TimeLogJustificationService>();
builder.Services.AddScoped<IOvertimeRequestService, OvertimeRequestService>();
builder.Services.AddScoped<IDutyExchangeRequestService, DutyExchangeRequestService>();
builder.Services.AddScoped<IExchangeRestDayRequestService, ExchangeRestDayRequestService>();
builder.Services.AddScoped<IUndertimeRequestService, UndertimeRequestService>();
builder.Services.AddScoped<IOvertimeEngineService, OvertimeEngineService>();
builder.Services.AddScoped<IWorkingCalendarService, WorkingCalendarService>();
builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();

// ============================================================
// COOKIE CONFIGURATION
// ============================================================
builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    // Paths
    options.LoginPath = "/Account/Index";
    options.AccessDeniedPath = "/Account/AccessDenied";

    // Performance: Reduce cookie validation frequency
    options.Events.OnValidatePrincipal = async context =>
    {
        // Validate every 30 minutes instead of every request
        var userPrincipal = context.Principal;
        var lastValidated = userPrincipal?.FindFirst("LastValidated")?.Value;

        if (lastValidated != null && DateTime.TryParse(lastValidated, out var validatedTime))
        {
            if (DateTime.UtcNow.Subtract(validatedTime).TotalMinutes < 30)
            {
                return; // Skip validation
            }
        }

        // Update last validated time
        await SecurityStampValidator.ValidatePrincipalAsync(context);
    };
});

// ============================================================
// AUTHORIZATION
// ============================================================
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission", policyBuilder =>
    {
        policyBuilder.Requirements.Add(new PermissionAuthorizationRequirement());
    });
});

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, MyUserClaimsPrincipalFactory>();

// ============================================================
// AUTHENTICATION
// ============================================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
});

// ============================================================
// RAZOR PAGES & AUTO MAPPER
// ============================================================
builder.Services.AddRazorPages();

// ============================================================
// SESSION
// ============================================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ============================================================
// CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// ============================================================
// HTTP CLIENT (Optimized connection pooling)
// ============================================================
builder.Services.AddHttpClient("default", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxConnectionsPerServer = 10,
    UseProxy = false
});

// Add SignalR services
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 102400; // 100KB
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.StreamBufferCapacity = 10;
});

var app = builder.Build();

// ============================================================
// PRE-LOAD CRITICAL DATA INTO CACHE ON STARTUP
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("=== Application Startup: Pre-loading critical data ===");
    var startTime = DateTime.UtcNow;

    try
    {
        // 1. Pre-load SystemInfo
        logger.LogInformation("Pre-loading SystemInfo...");
        var systemInfo = await dbContext.SystemInfos
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync();

        if (systemInfo != null)
        {
            cache.Set("SystemInfo", systemInfo, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.High,
                Size = 1
            });
            logger.LogInformation("? SystemInfo pre-loaded: {SystemName}", systemInfo.SystemName);
        }
        else
        {
            logger.LogWarning("? SystemInfo not found during pre-loading");
        }

        // 2. Pre-load active roles and their permissions (NEW - CRITICAL FOR PERFORMANCE)
        logger.LogInformation("Pre-loading roles and permissions...");
        var permissionCache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();

        var activeRoles = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.IsActive && !r.IsDeleted)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        logger.LogInformation("Found {Count} active roles", activeRoles.Count);

        // Pre-load permissions for each role
        foreach (var role in activeRoles)
        {
            var permissions = await permissionCache.GetRolePermissionsAsync(role.Id);
            logger.LogInformation("? Cached {Count} permissions for role: {RoleName}",
                permissions.Count, role.Name);
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogInformation("=== Startup cache pre-loading completed in {Duration}ms ===", duration);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during startup cache pre-loading");
        // Don't throw - allow app to start even if cache pre-loading fails
    }
}

// ============================================================
// MIDDLEWARE PIPELINE - OPTIMIZED ORDER
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseResponseCompression();

app.UseHttpsRedirection();

// Static files with caching
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 30 days
        if (!ctx.Context.Request.Path.StartsWithSegments("/api"))
        {
            const int durationInSeconds = 60 * 60 * 24 * 30; // 30 days
            ctx.Context.Response.Headers["Cache-Control"] =
                $"public,max-age={durationInSeconds}";
            ctx.Context.Response.Headers["Expires"] = DateTime.UtcNow.AddSeconds(durationInSeconds).ToString("R");
        }
    }
});

app.UseCookiePolicy();
app.UseRouting();

// Optional: Enable response caching
// app.UseResponseCaching();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Index}/{id?}");


// ============================================================
// DATABASE INITIALIZATION & SEEDING
// ============================================================
try
{
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting database initialization...");

        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        dbInitializer.Initialize();

        logger.LogInformation("Database initialization completed successfully");

        // Seed sample data for testing
        logger.LogInformation("Starting sample data seeding...");
        await SeedData.Initialize(scope.ServiceProvider);
        logger.LogInformation("Sample data seeding completed successfully");
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database seeding failed: {Message}", ex.Message);
    throw; // Re-throw if you want the application to fail
}

// ============================================================
// BACKGROUND WARM-UP (Optional - runs after startup)
// ============================================================
_ = Task.Run(async () =>
{
    try
    {
        // Wait for app to fully start
        await Task.Delay(TimeSpan.FromSeconds(3));

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var warmUpService = scope.ServiceProvider.GetRequiredService<IStartupOptimizationService>();

        logger.LogInformation("Starting background warm-up...");
        await warmUpService.WarmUpAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Background warm-up failed (non-critical)");
    }
});


app.Run();