using System.Windows.Controls;
using WpfFoundation.Gallery.ViewModels;

namespace WpfFoundation.Gallery.Pages;

public partial class BehaviorsPage : UserControl
{
    public BehaviorsPage()
    {
        InitializeComponent();
        DataContext = new BehaviorsViewModel();
    }
}
