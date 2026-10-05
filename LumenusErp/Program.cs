using LumenusErp.Components;
using LumenusErp.Components.Account;
using LumenusErp.Data;
using LumenusErp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// Кириллица в <title>/мета-тегах как есть, а не &#x...; — сырой HTML читают AI-краулеры
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.Cyrillic, UnicodeRanges.GeneralPunctuation));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var connectionStringAos = builder.Configuration.GetConnectionString("AosConnection") ?? throw new InvalidOperationException("Connection string 'AosConnection' not found.");

// Фабрика нужна Blazor-компонентам (короткоживущий контекст на операцию); Identity и остальной
// код получают обычный scoped-контекст, созданный той же фабрикой.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddDbContext<AosDbContext>(options =>
    options.UseNpgsql(connectionStringAos));

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<AiLimitsOptions>(builder.Configuration.GetSection("AiLimits"));
builder.Services.AddSingleton<AiRateLimiter>();
builder.Services.AddSingleton<AiPromptStore>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<UserApiTokenService>();

// Вкладка «Созвоны»: загрузка → ffmpeg → myasi → LLM, обработка по одной записи в фоне
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddSingleton<CallSettings>();
builder.Services.AddSingleton<CallQueue>();
builder.Services.AddSingleton<CallService>();
builder.Services.AddHostedService<CallProcessor>();
// Распознавание медленное (кусок 10 минут аудио), поэтому таймаут большой
builder.Services.AddHttpClient(CallProcessor.HttpClientName, client =>
{
    var baseUrl = builder.Configuration["Myasi:BaseUrl"];
    client.BaseAddress = new Uri((string.IsNullOrWhiteSpace(baseUrl) ? "http://myasi:8000" : baseUrl.Trim()).TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(15);
    // Служебный токен myasi (его TRANSCRIBE_TOKEN); пусто — myasi ответит 404/401, запись получит понятную ошибку
    var token = builder.Configuration["Myasi:Token"];
    if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new("Bearer", token.Trim());
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();



var app = builder.Build();

LumenusErp.MySec.Configure(app.Configuration["Api:Token"]);
AiModule.Configure(app.Configuration, app.Services.GetRequiredService<AiPromptStore>());

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var aosDbContext = scope.ServiceProvider.GetRequiredService<AosDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    await dbContext.Database.MigrateAsync();
    await aosDbContext.Database.MigrateAsync();
    await ProjectSeed.EnsureSeededAsync(dbContext);
    await AiPromptSeed.EnsureSeededAsync(dbContext);

    // ── Засидировать роли ────────────────────────────────────────────
    var roles = new[] { "Admin", "Manager", "User", "Ghost", "Aos" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ── Создать админа по умолчанию (если нет) ───────────────────────
    var adminEmail = builder.Configuration["Admin:Email"] ?? "rrovensky@mail.ru";
    var adminPassword = builder.Configuration["Admin:Password"] ?? "Admin123!";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
        else
        {
            app.Logger.LogWarning("Не удалось создать администратора {Email}: {Errors}",
                adminEmail, string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }
    }
    else if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}
// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

// Трекер задач живёт на отдельном поддомене (Tasks:Host): на нём / ведёт в /tasks, на остальных хостах /tasks* = 404.
// Пустой Tasks:Host — без ограничений. API, Account, статику и т. п. не трогаем.
var tasksHost = app.Configuration["Tasks:Host"]?.Trim();
if (!string.IsNullOrEmpty(tasksHost))
{
    app.Use(async (context, next) =>
    {
        var onTasksHost = string.Equals(context.Request.Host.Host, tasksHost, StringComparison.OrdinalIgnoreCase);
        var path = context.Request.Path;
        if (onTasksHost && HttpMethods.IsGet(context.Request.Method) && path == "/")
        {
            context.Response.Redirect("/tasks");
            return;
        }
        if (!onTasksHost && (path.Equals("/tasks", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/tasks", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

// MapStaticAssets отдаёт .txt как "text/plain" без кодировки — для robots.txt
// клиенты тогда могут не угадать UTF-8 и показать кириллицу кракозябрами.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Response.OnStarting(() =>
        {
            if (string.Equals(context.Response.ContentType, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
            }
            return Task.CompletedTask;
        });
    }
    await next();
});

app.UseSwaggerUI();
app.UseSwagger();
// Явно, а не автоматически (WebApplication иначе ставит их в самое начало конвейера): иначе анонимный запрос
// к [Authorize]-странице получил бы редирект на логин раньше, чем host-middleware ответит 404.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapSeoEndpoints();
app.MapMediaEndpoints();
app.MapCallEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
