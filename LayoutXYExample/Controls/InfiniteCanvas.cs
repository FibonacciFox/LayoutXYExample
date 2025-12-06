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

    // ВАЖНО: Nodify регистрирует свойства с новым TransformGroup().
    // Но обновлять их мы будем через SetCurrentValue, заменяя объект целиком.
    public static readonly StyledProperty<Transform> ViewportTransformProperty =
        AvaloniaProperty.Register<InfiniteCanvas, Transform>(nameof(ViewportTransform), new TransformGroup());

    public static readonly StyledProperty<Transform> DpiScaledViewportTransformProperty =
        AvaloniaProperty.Register<InfiniteCanvas, Transform>(nameof(DpiScaledViewportTransform), new TransformGroup());

    // Настройки
    public static readonly StyledProperty<double> MinZoomProperty = 
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MinZoom), 0.1);
    public static readonly StyledProperty<double> MaxZoomProperty = 
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MaxZoom), 5.0);

    #endregion

    #region Property Wrappers

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

    public double MinZoom { get => GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    #endregion

    #region Internal Transforms

    // Мы храним сами объекты трансформаций (Scale и Translate) как поля класса.
    // Это позволяет нам менять их значения (.X, .Y, .ScaleX), 
    // но при обновлении свойства мы будем оборачивать их в НОВЫЙ TransformGroup.
    private readonly TranslateTransform _translateTransform = new TranslateTransform();
    private readonly ScaleTransform _scaleTransform = new ScaleTransform();
    private readonly TranslateTransform _dpiTranslateTransform = new TranslateTransform();

    #endregion

    #region State

    private bool _isPanning;
    private Point _panStartMousePosition;
    private Point _panStartViewportLocation;

    #endregion

    static InfiniteCanvas()
    {
        FocusableProperty.OverrideDefaultValue<InfiniteCanvas>(true);

        // При изменении данных вызываем пересчет визуальных трансформаций
        ViewportLocationProperty.Changed.AddClassHandler<InfiniteCanvas>((x, e) => x.UpdateTransforms());
        ViewportZoomProperty.Changed.AddClassHandler<InfiniteCanvas>((x, e) => x.UpdateTransforms());
    }

    public InfiniteCanvas()
    {
        // Принудительно вызываем обновление при старте, чтобы создать начальные группы
        UpdateTransforms();
        
        // Подписка на изменение DPI (если перетащили окно на другой монитор)
        this.EffectiveViewportChanged += (_, _) => UpdateTransforms();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTransforms();
    }

    /// <summary>
    /// Этот метод реализует логику из NodifyEditor.cs -> UpdateViewportTransform.
    /// КЛЮЧЕВОЙ МОМЕНТ: Мы создаем new TransformGroup() каждый раз.
    /// Это заставляет систему биндинга Avalonia увидеть изменение свойства и обновить DrawingBrush.
    /// </summary>
    private void UpdateTransforms()
    {
        // 1. Обновляем значения во внутренних объектах
        _scaleTransform.ScaleX = ViewportZoom;
        _scaleTransform.ScaleY = ViewportZoom;

        double x = -ViewportLocation.X * ViewportZoom;
        double y = -ViewportLocation.Y * ViewportZoom;

        _translateTransform.X = x;
        _translateTransform.Y = y;

        // 2. Считаем DPI-коррекцию (Workaround из issue #11959)
        var root = this.GetVisualRoot();
        double renderScaling = root?.RenderScaling ?? 1.0;

        // Важно: Snap to pixels (округление до ближайшего физического пикселя)
        // Это предотвращает размытие линий сетки.
        _dpiTranslateTransform.X = Math.Round(x * renderScaling) / renderScaling;
        _dpiTranslateTransform.Y = Math.Round(y * renderScaling) / renderScaling;

        // 3. СОЗДАЕМ НОВЫЕ ГРУППЫ (как в Nodify)
        // Это решает проблему "статичной сетки". Если не пересоздать группу, 
        // Binding на Brush может не подхватить изменения внутри существующей группы.
        
        var viewportGroup = new TransformGroup();
        viewportGroup.Children.Add(_scaleTransform);
        viewportGroup.Children.Add(_translateTransform);
        
        // Присваиваем новое значение свойству
        SetCurrentValue(ViewportTransformProperty, viewportGroup);

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        
        // Присваиваем новое значение свойству
        SetCurrentValue(DpiScaledViewportTransformProperty, dpiGroup);
    }

    #region Input Handling

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.Handled) return;

        double zoomFactor = 1.1;
        double prevZoom = ViewportZoom;
        double newZoom = e.Delta.Y > 0 ? prevZoom * zoomFactor : prevZoom / zoomFactor;

        newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

        if (Math.Abs(newZoom - prevZoom) > 0.001)
        {
            Point mousePos = e.GetPosition(this);

            // Логика зума в точку курсора
            Vector correction = (Vector)mousePos / prevZoom - (Vector)mousePos / newZoom;

            ViewportZoom = newZoom;
            ViewportLocation += correction;
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
        Vector diffScreen = _panStartMousePosition - currentMousePos;

        // Пересчет экранного сдвига в логический
        ViewportLocation = _panStartViewportLocation + (diffScreen / ViewportZoom);
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

    #endregion
}