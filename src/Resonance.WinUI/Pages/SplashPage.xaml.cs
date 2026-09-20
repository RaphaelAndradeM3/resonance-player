using Microsoft.UI.Xaml.Controls;
using Resonance.WinUI.Controls;

namespace Resonance.WinUI.Pages;

public sealed partial class SplashPage : Page, ICustomTitleBarProvider
{
    public SplashPage()
    {
        this.InitializeComponent();
        this.Loaded += (_, _) =>
        {
            LogoEntrance.Begin();
            TitleEntrance.Begin();
            StatusEntrance.Begin();
        };
    }

    public TitleBar GetAppTitleBarElement()
    {
        return AppTitleBar;
    }

    public RowDefinition GetAppTitleBarRowElement()
    {
        return AppTitleBarRow;
    }
}
