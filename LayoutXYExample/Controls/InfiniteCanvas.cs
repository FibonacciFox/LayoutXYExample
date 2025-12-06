using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace LayoutXYExample.Controls;

public class InfiniteCanvas : ContentControl
{
    #region Dependency Properties

    public static readonly StyledProperty<Point> ViewportLocationProperty =
        AvaloniaProperty.Register<InfiniteCanvas, Point>(nameof(ViewportLocation));

    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(ViewportZoom), 1.0);

    public static readonly StyledProperty<Transform> ViewportTransformProperty =
        AvaloniaProperty.Register<InfiniteCanvas, Transform>(nameof(ViewportTransform));

    public static readonly StyledProperty<Transform> DpiScaledViewportTransformProperty =
        AvaloniaProperty.Register<InfiniteCanvas, Transform>(nameof(DpiScaledViewportTransform), new TransformGroup());

    public static readonly StyledProperty<double> MinZoomProperty = 
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MinZoom), 0.1);
    public static readonly StyledProperty<double> MaxZoomProperty = 
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MaxZoom), 5.0);

    #endregion

    #region Property Wrappers
    
    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public Point ViewportLocation
    {
        get => GetValue(ViewportLocationProperty);
        set => SetValue(ViewportLocationProperty, value);
    }

    public double ViewportZoom
    {
        get => GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public Transform ViewportTransform
    {
        get => GetValue(ViewportTransformProperty);
        set => SetValue(ViewportTransformProperty, value);
    }

    public Transform DpiScaledViewportTransform
    {
        get => GetValue(DpiScaledViewportTransformProperty);
        set => SetValue(DpiScaledViewportTransformProperty, value);
    }

    #endregion

    #region Internal Transforms

    private readonly TranslateTransform _translateTransform = new();
    private readonly ScaleTransform _scaleTransform = new();
    private readonly TranslateTransform _dpiTranslateTransform = new();

    #endregion

    #region State

    private bool _isPanning;
    private Point _panStartMousePosition;
    private Point _panStartViewportLocation;

    #endregion

    static InfiniteCanvas()
    {
        FocusableProperty.OverrideDefaultValue<InfiniteCanvas>(true);
        ViewportLocationProperty.Changed.AddClassHandler<InfiniteCanvas>((x, e) => x.UpdateTransforms());
        ViewportZoomProperty.Changed.AddClassHandler<InfiniteCanvas>((x, e) => x.UpdateTransforms());
    }

    public InfiniteCanvas()
    {
        // 1. Transform для Контента
        var group = new TransformGroup();
        group.Children.Add(_scaleTransform);
        group.Children.Add(_translateTransform);
        ViewportTransform = group;

        // Обязательно инициализируем DPI матрицу, чтобы Binding в XAML не получил null
        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        DpiScaledViewportTransform = dpiGroup;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTransforms();
    }

    private void UpdateTransforms()
    {
        // Обновляем Scale
        _scaleTransform.ScaleX = ViewportZoom;
        _scaleTransform.ScaleY = ViewportZoom;

        // Обновляем Translate (-Location * Zoom)
        double x = -ViewportLocation.X * ViewportZoom;
        double y = -ViewportLocation.Y * ViewportZoom;

        _translateTransform.X = x;
        _translateTransform.Y = y;

        // Обновляем DPI Translate
        double dpi = this.GetVisualRoot()?.RenderScaling ?? 1.0;
        _dpiTranslateTransform.X = x * dpi;
        _dpiTranslateTransform.Y = y * dpi;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.Handled) return;

        double zoomFactor = 1.1;
        double newZoom = e.Delta.Y > 0 ? ViewportZoom * zoomFactor : ViewportZoom / zoomFactor;
        newZoom = Math.Max(GetValue(MinZoomProperty), Math.Min(GetValue(MaxZoomProperty), newZoom));

        if (Math.Abs(newZoom - ViewportZoom) > 0.001)
        {
            Point mousePos = e.GetPosition(this);
            Vector shift = (Vector)mousePos / ViewportZoom - (Vector)mousePos / newZoom;
            
            ViewportZoom = newZoom;
            ViewportLocation += shift;
        }
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning = true;
            _panStartMousePosition = e.GetPosition(this);
            _panStartViewportLocation = ViewportLocation;
            
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPanning) return;

        Point currentMousePos = e.GetPosition(this);
        Vector diff = _panStartMousePosition - currentMousePos;
        ViewportLocation = _panStartViewportLocation + (diff / ViewportZoom);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = Cursor.Default;
        }
    }
}