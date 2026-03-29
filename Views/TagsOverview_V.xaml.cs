using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using TagExplorer.ViewModels;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Tags_V.xaml
    /// </summary>
    public partial class TagsOverview_V : UserControl
    {
        public TagsOverview_V()
        {
            InitializeComponent();

            DataContext = App.AppHost!.Services.GetRequiredService<TagsOverview_VM>();
        }
    }
}
