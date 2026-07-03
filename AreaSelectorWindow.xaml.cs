using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameTranslatorLens.Core;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DrawingPoint = System.Drawing.Point;
using FormsCursor = System.Windows.Forms.Cursor;
using Point = System.Windows.Point;

namespace GameTranslatorLens;

public partial class AreaSelectorWindow : Window
{
    private Point _start;
    private DrawingPoint _startScreen;
    private bool _drawing;

    public event EventHandler<Rect>? SelectionCompleted;

    public AreaSelectorWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => Activate();
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _startScreen = FormsCursor.Position;
        _drawing = true;
        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        Point current = e.GetPosition(this);
        double left = Math.Min(_start.X, current.X);
        double top = Math.Min(_start.Y, current.Y);
        SelectionRect.Width = Math.Abs(current.X - _start.X);
        SelectionRect.Height = Math.Abs(current.Y - _start.Y);
        Canvas.SetLeft(SelectionRect, left);
        Canvas.SetTop(SelectionRect, top);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        ReleaseMouseCapture();
        _drawing = false;
        DrawingPoint currentScreen = FormsCursor.Position;
        double screenLeft = Math.Min(_startScreen.X, currentScreen.X);
        double screenTop = Math.Min(_startScreen.Y, currentScreen.Y);
        double screenWidth = Math.Abs(currentScreen.X - _startScreen.X);
        double screenHeight = Math.Abs(currentScreen.Y - _startScreen.Y);
        if (screenWidth < ScreenBoundsService.MinimumCaptureWidth || screenHeight < ScreenBoundsService.MinimumCaptureHeight)
        {
            Close();
            return;
        }

        Rect rect = new(screenLeft, screenTop, screenWidth, screenHeight);
        if (!ScreenBoundsService.TryClipToVirtualScreen(rect, out Rect clipped))
        {
            System.Windows.MessageBox.Show(
                "选择的聊天区域太小或不在当前屏幕范围内，请重新选择聊天区域。",
                "Game Translator Lens",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
            return;
        }

        SelectionCompleted?.Invoke(this, clipped);
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
