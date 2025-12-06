using Avalonia.Controls;
using Avalonia.Input;
using LayoutXYExample.ViewModels;

namespace LayoutXYExample.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        DataContext = new LayoutViewModel();
    }

    // Прокси-методы для перенаправления событий во ViewModel
    
    private void OnPointerPressedProxy(object sender, PointerPressedEventArgs e)
    {
        if (DataContext is LayoutViewModel vm) vm.OnPointerPressed(sender, e);
    }

    private void OnPointerMovedProxy(object sender, PointerEventArgs e)
    {
        if (DataContext is LayoutViewModel vm) vm.OnPointerMoved(sender, e);
    }

    private void OnPointerReleasedProxy(object sender, PointerReleasedEventArgs e)
    {
        if (DataContext is LayoutViewModel vm) vm.OnPointerReleased(sender, e);
    }
}