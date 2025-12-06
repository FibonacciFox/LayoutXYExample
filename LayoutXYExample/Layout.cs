using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LayoutXYExample;

/// <summary>
/// Система абсолютного позиционирования.
/// <para>Логика Stretch:</para>
/// <para>1. При установке Stretch -> Width/Height сбрасываются в NaN (Auto).</para>
/// <para>2. При перемещении Stretch-элемента -> Width/Height фиксируются, Alignment меняется на Left/Top.</para>
/// </summary>
public static class Layout
{
    private static int _isInsidePositionChange;
    
    [ThreadStatic]
    private static bool _isUpdatingReadOnlyValues;

    #region Attached Properties

    public static readonly AttachedProperty<double> XProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "X", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> YProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "Y", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> DesignXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignX", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> DesignYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignY", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    #endregion

    static Layout()
    {
        XProperty.Changed.Subscribe((AvaloniaPropertyChangedEventArgs<double> e) => OnPositionChanged(e.Sender as Control));
        YProperty.Changed.Subscribe((AvaloniaPropertyChangedEventArgs<double> e) => OnPositionChanged(e.Sender as Control));
        
        DesignXProperty.Changed.Subscribe((AvaloniaPropertyChangedEventArgs<double> e) => OnDesignPositionChanged(e.Sender as Control));
        DesignYProperty.Changed.Subscribe((AvaloniaPropertyChangedEventArgs<double> e) => OnDesignPositionChanged(e.Sender as Control));

        Layoutable.HorizontalAlignmentProperty.Changed.Subscribe(e => OnAlignmentChanged(e.Sender as Control));
        Layoutable.VerticalAlignmentProperty.Changed.Subscribe(e => OnAlignmentChanged(e.Sender as Control));
    }

    private static void OnPositionChanged(Control? control)
    {
        if (control == null) return;

        if (_isUpdatingReadOnlyValues)
        {
            UpdateDesignPosition(control);
            return;
        }

        if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;

        try
        {
            control.LayoutUpdated -= OnLayoutUpdated;
            control.LayoutUpdated += OnLayoutUpdated;

            EnsureManualPositioningMode(control);

            ApplyPosition(control);
            UpdateDesignPosition(control);
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    private static void OnDesignPositionChanged(Control? control)
    {
        if (control == null || Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
        
        try
        {
            if (control.GetVisualRoot() is null)
            {
                void Handler(object? s, VisualTreeAttachmentEventArgs e)
                {
                    control.AttachedToVisualTree -= Handler;
                    Dispatcher.UIThread.Post(() => OnDesignPositionChanged(control), DispatcherPriority.Loaded);
                }
                control.AttachedToVisualTree += Handler;
                return;
            }

            Visual? reference = control.FindAncestorOfType<UiDesigner>() as Visual ?? control.GetVisualRoot() as Visual;
            var parent = control.GetVisualParent();
            
            if (reference == null || parent == null) return;

            var dx = GetDesignX(control);
            var dy = GetDesignY(control);

            if (!double.IsNaN(dx) && !double.IsNaN(dy))
            {
                var local = new Point(dx, dy);
                var translated = reference.TranslatePoint(local, parent);

                if (translated.HasValue)
                {
                    SetX(control, translated.Value.X);
                    SetY(control, translated.Value.Y);
                    
                    EnsureManualPositioningMode(control);

                    ApplyPosition(control);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    /// <summary>
    /// Логика "Ручного режима": Если тянем мышкой, фиксируем размер и ставим Left/Top.
    /// </summary>
    private static void EnsureManualPositioningMode(Control control)
    {
        if (IsInsideCanvas(control)) return;

        bool hasX = !double.IsNaN(GetX(control));
        bool hasY = !double.IsNaN(GetY(control));

        // --- Горизонталь ---
        if (hasX)
        {
            if (control.HorizontalAlignment == HorizontalAlignment.Stretch)
            {
                // Фиксируем ширину перед тем, как убрать Stretch
                if (double.IsNaN(control.Width)) control.Width = control.Bounds.Width;
                control.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else if (control.HorizontalAlignment != HorizontalAlignment.Left)
            {
                control.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        // --- Вертикаль ---
        if (hasY)
        {
            if (control.VerticalAlignment == VerticalAlignment.Stretch)
            {
                // Фиксируем высоту перед тем, как убрать Stretch
                if (double.IsNaN(control.Height)) control.Height = control.Bounds.Height;
                control.VerticalAlignment = VerticalAlignment.Top;
            }
            else if (control.VerticalAlignment != VerticalAlignment.Top)
            {
                control.VerticalAlignment = VerticalAlignment.Top;
            }
        }
    }

    /// <summary>
    /// Логика "Смены выравнивания": Если ставим Stretch, сбрасываем размер в Auto.
    /// </summary>
    private static void OnAlignmentChanged(Control? control)
    {
        if (control == null) return;

        // Если пользователь выбрал Stretch, он ожидает, что элемент растянется.
        // Для этого нужно сбросить фиксированные размеры.
        if (control.HorizontalAlignment == HorizontalAlignment.Stretch)
            control.Width = double.NaN;

        if (control.VerticalAlignment == VerticalAlignment.Stretch)
            control.Height = double.NaN;

        ApplyPosition(control);
        Dispatcher.UIThread.Post(() => OnLayoutUpdated(control, EventArgs.Empty), DispatcherPriority.Loaded);
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;
        if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;

        try
        {
            bool isManuallyPositioned = IsInsideCanvas(control) || 
                                        (control.HorizontalAlignment == HorizontalAlignment.Left &&
                                         control.VerticalAlignment == VerticalAlignment.Top &&
                                         !double.IsNaN(GetX(control)) && !double.IsNaN(GetY(control)));
            
            if (!isManuallyPositioned)
            {
                var parent = control.GetVisualParent();
                if (parent != null)
                {
                    var pos = control.TranslatePoint(new Point(0, 0), parent);
                    if (pos.HasValue)
                    {
                        _isUpdatingReadOnlyValues = true;
                        try 
                        {
                            if (Math.Abs(GetX(control) - pos.Value.X) > 0.01) SetX(control, pos.Value.X);
                            if (Math.Abs(GetY(control) - pos.Value.Y) > 0.01) SetY(control, pos.Value.Y);
                        }
                        finally
                        {
                            _isUpdatingReadOnlyValues = false;
                        }
                    }
                }
            }
            UpdateDesignPosition(control);
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    private static void ApplyPosition(Control control)
    {
        var x = GetX(control);
        var y = GetY(control);

        if (IsInsideCanvas(control))
        {
            if (!double.IsNaN(x)) Canvas.SetLeft(control, x);
            if (!double.IsNaN(y)) Canvas.SetTop(control, y);
            if (control.RenderTransform is TranslateTransform) control.RenderTransform = null;
        }
        else
        {
            var transform = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
            
            transform.X = (control.HorizontalAlignment == HorizontalAlignment.Left && !double.IsNaN(x)) ? x : 0;
            transform.Y = (control.VerticalAlignment == VerticalAlignment.Top && !double.IsNaN(y)) ? y : 0;
            
            control.RenderTransform = transform;
        }
    }

    private static void UpdateDesignPosition(Control control)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
            try
            {
                Visual? reference = control.FindAncestorOfType<UiDesigner>() as Visual ?? control.GetVisualRoot() as Visual;
                if (reference != null)
                {
                    var position = control.TranslatePoint(new Point(0, 0), reference);
                    if (position.HasValue)
                    {
                        double currentDX = GetDesignX(control);
                        double currentDY = GetDesignY(control);
                        if (Math.Abs(currentDX - position.Value.X) > 0.01) SetDesignX(control, position.Value.X);
                        if (Math.Abs(currentDY - position.Value.Y) > 0.01) SetDesignY(control, position.Value.Y);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isInsidePositionChange, 0);
            }
        }, DispatcherPriority.Input);
    }

    private static bool IsInsideCanvas(Control control) => control.GetVisualParent() is Canvas;

    #region Accessors
    public static double GetX(AvaloniaObject obj) => obj.GetValue(XProperty);
    public static void SetX(AvaloniaObject obj, double value) => obj.SetValue(XProperty, value);
    public static double GetY(AvaloniaObject obj) => obj.GetValue(YProperty);
    public static void SetY(AvaloniaObject obj, double value) => obj.SetValue(YProperty, value);
    public static double GetDesignX(AvaloniaObject obj) => obj.GetValue(DesignXProperty);
    public static void SetDesignX(AvaloniaObject obj, double value) => obj.SetValue(DesignXProperty, value);
    public static double GetDesignY(AvaloniaObject obj) => obj.GetValue(DesignYProperty);
    public static void SetDesignY(AvaloniaObject obj, double value) => obj.SetValue(DesignYProperty, value);
    #endregion
}