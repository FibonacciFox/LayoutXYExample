using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout; // Важно для Enum
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace LayoutXYExample.ViewModels;

public partial class LayoutViewModel : ObservableObject
{
    // --- Координаты ---
    [ObservableProperty] private double _elementX = double.NaN;
    [ObservableProperty] private double _elementY = double.NaN;
    [ObservableProperty] private double _designX = 50;
    [ObservableProperty] private double _designY = 50;

    // --- Размеры ---
    [ObservableProperty] private double _elementWidth = 100;
    [ObservableProperty] private double _elementHeight = 50;

    // --- Выравнивание (Тип Enum!) ---
    [ObservableProperty] private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
    [ObservableProperty] private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;

    public HorizontalAlignment[] AlignmentOptions { get; } = Enum.GetValues<HorizontalAlignment>();
    public VerticalAlignment[] VerticalAlignmentOptions { get; } = Enum.GetValues<VerticalAlignment>();

    // --- Перетаскивание ---
    private bool _isDragging = false;
    private Point _startPoint;

    // --- Команды ---

    [RelayCommand]
    private void ResetPosition()
    {
        ElementX = 0;  
        ElementY = 0;
        ElementWidth = 100;
        ElementHeight = 50;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    [RelayCommand]
    private void SetAutoWidth() => ElementWidth = double.NaN;

    [RelayCommand]
    private void SetAutoHeight() => ElementHeight = double.NaN;

    // --- Публичные методы для привязки событий (Proxy) ---

    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        if (point.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            var visual = e.Source as Visual;
            
            if (visual?.GetVisualRoot() is Visual root)
            {
                 _startPoint = e.GetCurrentPoint(root).Position;
            }

            if (visual is IInputElement inputElement)
            {
                e.Pointer.Capture(inputElement);
            }
            e.Handled = true;
        }
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var visual = e.Source as Visual;
        if (visual?.GetVisualRoot() is Visual root)
        {
            var currentPoint = e.GetCurrentPoint(root).Position;
            var delta = currentPoint - _startPoint;

            DesignX += delta.X;
            DesignY += delta.Y;

            _startPoint = currentPoint;
        }
        e.Handled = true;
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}