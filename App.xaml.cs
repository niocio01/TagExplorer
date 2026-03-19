using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using TagExplorer.Data;
using TagExplorer.Services;
using TagExplorer.ViewModels;
using TagExplorer.Views;
using SystemColors = TagExplorer.Data.SystemColors;

namespace TagExplorer;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }
    public IServiceProvider ServiceProvider { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(hostConfig =>
            {
                hostConfig.SetBasePath(Directory.GetCurrentDirectory());
                hostConfig.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                hostConfig.AddEnvironmentVariables(prefix: "PREFIX_");
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<AppDbContext>();
                services.AddScoped<TagAssignmentService>();

                services.AddScoped<ViewModels.MainWindow_VM>();
                services.AddSingleton<MainWindow>(s => new MainWindow
                {
                    DataContext = s.GetRequiredService<MainWindow_VM>()
                });

                services.AddScoped<TagsOverview_VM>();
            })
            .Build();

        ServiceProvider = AppHost.Services;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();

        var db = ServiceProvider.GetRequiredService<AppDbContext>();
        var config = ServiceProvider.GetRequiredService<IConfiguration>();

        bool dbReady = await EnsureDatabaseReadyAsync(db, config);
        if (!dbReady)
        {
            Shutdown();
            return;
        }

        SystemColors.UpdateDefaultColors(db);
        SystemTags.UpdateSystemTags(db);

        MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();

        base.OnStartup(e);
    }

    private async Task<bool> EnsureDatabaseReadyAsync(AppDbContext db, IConfiguration config)
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.CloseConnectionAsync();
        }
        catch (Exception ex)
        {
            ShowDatabaseError(config, ex.Message, ex);
            return false;
        }

        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count == 0)
            return true;

        string migrationList = string.Join("\n• ", pendingMigrations);
        var result = MessageBox.Show(
            $"Database updates are required before startup.\n\n" +
            $"Pending migrations ({pendingMigrations.Count}):\n• {migrationList}\n\n" +
            "Apply migrations now?",
            "Database Migration Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return false;

        try
        {
            await db.Database.MigrateAsync();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError(config, "Applying migrations failed.", ex);
            return false;
        }
    }

    private void ShowDatabaseError(IConfiguration config, string errorDetails, Exception? exception = null)
    {
        string connectionString = config.GetConnectionString("database") ?? "Not configured";

        string safeConnectionString = connectionString;
        if (connectionString.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            var parts = connectionString.Split(';');
            safeConnectionString = string.Join(";", parts.Select(p =>
            {
                string trimmed = p.TrimStart();
                if (trimmed.StartsWith("Password", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
                {
                    int equalsIndex = trimmed.IndexOf('=');
                    return trimmed.Substring(0, equalsIndex + 1) + " ***";
                }
                return p;
            }));
        }

        string errorMessage = $"Could not connect to the database.\n\n" +
            $"Error Details:\n{errorDetails}\n\n" +
            (exception?.InnerException != null ? $"Inner Error:\n{exception.InnerException.Message}\n\n" : "") +
            $"Connection String:\n{safeConnectionString}\n\n" +
            "Common solutions:\n" +
            "• Ensure PostgreSQL server is running\n" +
            "• Verify connection details in 'appsettings.json'\n" +
            "• Check if database exists (create it or run migrations)\n" +
            "• Verify username and password are correct\n" +
            "• Check firewall settings\n\n" +
            "Configure in: appsettings.json → ConnectionStrings → database";

        MessageBox.Show(
            errorMessage,
            "Database Connection Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
