using System.Windows;
using System.Windows.Input;
using DiscImageStudio.ViewModels;

namespace DiscImageStudio;

/// <summary>The easy mode dialog: one page that reads the disc, picks the preset and takes the
/// picture. The view model decides everything; this class wires the pickers and the close path.</summary>
public partial class EasyModeWindow : Window
{
    public EasyModeWindow(EasyModeViewModel viewModel, Func<string?> pickImage)
    {
        InitializeComponent();
        ViewModel = viewModel;
        viewModel.PickImage = pickImage;
        viewModel.CloseWithConfirm = () => DialogResult = true;
        DataContext = viewModel;

        SourceInitialized += (_, _) => WindowCorners.Apply(this);
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Close();
            }
        };
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    public EasyModeViewModel ViewModel { get; }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Shows the dialog over its owner and returns the confirmed result, or null.</summary>
    public static EasyModeResult? Show(Window owner, EasyModeViewModel viewModel, Func<string?> pickImage)
    {
        EasyModeWindow window = new(viewModel, pickImage) { Owner = owner };
        return window.ShowDialog() == true ? viewModel.Result : null;
    }
}
