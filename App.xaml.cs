using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.IO;
using System.Windows;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.ViewModels;
using TagExplorer.Views;
using SystemColors = TagExplorer.Data.SystemColors;

namespace TagExplorer
{
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
                    services.AddScoped<MainWindow_VM>();
                    services.AddSingleton<MainWindow>(s => new MainWindow()
                    {
                        DataContext = s.GetRequiredService<MainWindow_VM>()
                    });

                    services.AddScoped<TagsOverview_VM>();
                    services.AddDbContext<AppDbContext>();
                })
                .Build();

            ServiceProvider = AppHost.Services;

            var db = AppHost.Services.GetRequiredService<AppDbContext>();
            SystemColors.UpdateDefaultColors(db);
            SystemTags.UpdateSystemTags(db);


        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
            
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            
            base.OnExit(e);
        }
    }
}
