using System.Windows;

namespace LocalShare.App.Behaviors;

public static class AdaptiveLayoutBehavior
{
    public static readonly DependencyProperty CompactWidthThresholdProperty =
        DependencyProperty.RegisterAttached("CompactWidthThreshold", typeof(double), typeof(AdaptiveLayoutBehavior), new PropertyMetadata(700.0, OnThresholdChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(AdaptiveLayoutBehavior), new PropertyMetadata(false));

    public static double GetCompactWidthThreshold(DependencyObject obj) => (double)obj.GetValue(CompactWidthThresholdProperty);
    public static void SetCompactWidthThreshold(DependencyObject obj, double value) => obj.SetValue(CompactWidthThresholdProperty, value);

    public static bool GetIsCompact(DependencyObject obj) => (bool)obj.GetValue(IsCompactProperty);
    public static void SetIsCompact(DependencyObject obj, bool value) => obj.SetValue(IsCompactProperty, value);

    private static void OnThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window)
        {
            window.SizeChanged -= Window_SizeChanged;
            window.SizeChanged += Window_SizeChanged;
        }
    }

    private static void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Window window)
        {
            double threshold = GetCompactWidthThreshold(window);
            SetIsCompact(window, window.ActualWidth < threshold);
        }
    }
}
