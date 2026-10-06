using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FinPulse.Windows.Services;

/// <summary>
/// Provides attached properties and dynamic localization services for WinUI 3 XAML controls.
/// Automatically updates localized strings across all active views when language changes at runtime,
/// without requiring application restart or depending on package identity.
/// </summary>
public static class Loc
{
    private static readonly List<WeakReference<FrameworkElement>> RegisteredElements = new();
    private static bool _isSubscribed;

    #region Attached Properties

    public static readonly DependencyProperty UidProperty =
        DependencyProperty.RegisterAttached(
            "Uid",
            typeof(string),
            typeof(Loc),
            new PropertyMetadata(null, OnUidChanged));

    public static string? GetUid(DependencyObject obj) => (string?)obj.GetValue(UidProperty);
    public static void SetUid(DependencyObject obj, string? value) => obj.SetValue(UidProperty, value);

    public static readonly DependencyProperty HeaderUidProperty =
        DependencyProperty.RegisterAttached(
            "HeaderUid",
            typeof(string),
            typeof(Loc),
            new PropertyMetadata(null, OnHeaderUidChanged));

    public static string? GetHeaderUid(DependencyObject obj) => (string?)obj.GetValue(HeaderUidProperty);
    public static void SetHeaderUid(DependencyObject obj, string? value) => obj.SetValue(HeaderUidProperty, value);

    public static readonly DependencyProperty PlaceholderUidProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderUid",
            typeof(string),
            typeof(Loc),
            new PropertyMetadata(null, OnPlaceholderUidChanged));

    public static string? GetPlaceholderUid(DependencyObject obj) => (string?)obj.GetValue(PlaceholderUidProperty);
    public static void SetPlaceholderUid(DependencyObject obj, string? value) => obj.SetValue(PlaceholderUidProperty, value);

    public static readonly DependencyProperty TooltipUidProperty =
        DependencyProperty.RegisterAttached(
            "TooltipUid",
            typeof(string),
            typeof(Loc),
            new PropertyMetadata(null, OnTooltipUidChanged));

    public static string? GetTooltipUid(DependencyObject obj) => (string?)obj.GetValue(TooltipUidProperty);
    public static void SetTooltipUid(DependencyObject obj, string? value) => obj.SetValue(TooltipUidProperty, value);

    #endregion

    static Loc()
    {
        EnsureSubscribed();
    }

    private static void EnsureSubscribed()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;
        LocalizationService.Current.LanguageChanged += (s, e) =>
        {
            RefreshAll();
        };
    }

    private static void OnUidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        EnsureSubscribed();
        if (d is FrameworkElement fe)
        {
            RegisterElement(fe);
            ApplyToElement(fe);
        }
    }

    private static void OnHeaderUidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        EnsureSubscribed();
        if (d is FrameworkElement fe)
        {
            RegisterElement(fe);
            ApplyHeaderToElement(fe);
        }
    }

    private static void OnPlaceholderUidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        EnsureSubscribed();
        if (d is FrameworkElement fe)
        {
            RegisterElement(fe);
            ApplyPlaceholderToElement(fe);
        }
    }

    private static void OnTooltipUidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        EnsureSubscribed();
        if (d is FrameworkElement fe)
        {
            RegisterElement(fe);
            ApplyTooltipToElement(fe);
        }
    }

    private static void RegisterElement(FrameworkElement element)
    {
        lock (RegisteredElements)
        {
            // Avoid duplicate registrations
            for (int i = 0; i < RegisteredElements.Count; i++)
            {
                if (RegisteredElements[i].TryGetTarget(out var target) && ReferenceEquals(target, element))
                {
                    return;
                }
            }
            RegisteredElements.Add(new WeakReference<FrameworkElement>(element));
        }
    }

    /// <summary>
    /// Applies localized strings to the given element based on all attached properties.
    /// </summary>
    public static void ApplyToElement(FrameworkElement element)
    {
        ApplyUidToElement(element);
        ApplyHeaderToElement(element);
        ApplyPlaceholderToElement(element);
        ApplyTooltipToElement(element);
    }

    private static void ApplyUidToElement(FrameworkElement element)
    {
        string? uid = GetUid(element);
        if (string.IsNullOrWhiteSpace(uid)) return;

        var loc = LocalizationService.Current;
        string text = loc.GetString(uid);
        if (string.IsNullOrEmpty(text) || text == uid) return;

        if (element is TextBlock tb)
        {
            tb.Text = text;
        }
        else if (element is AutoSuggestBox asb)
        {
            asb.PlaceholderText = text;
        }
        else if (element is TextBox txt)
        {
            if (uid.Contains("Header", StringComparison.OrdinalIgnoreCase))
                txt.Header = text;
            else
                txt.PlaceholderText = text;
        }
        else if (element is PasswordBox pb)
        {
            if (uid.Contains("Header", StringComparison.OrdinalIgnoreCase))
                pb.Header = text;
            else
                pb.PlaceholderText = text;
        }
        else if (element is ComboBox cb)
        {
            if (uid.Contains("Header", StringComparison.OrdinalIgnoreCase))
                cb.Header = text;
            else
                cb.PlaceholderText = text;
        }
        else if (element is InfoBar ib)
        {
            if (uid.Contains("Title", StringComparison.OrdinalIgnoreCase))
                ib.Title = text;
            else
                ib.Message = text;
        }
        else if (element is NavigationViewItem nvi)
        {
            nvi.Content = text;
        }
        else if (element is MenuFlyoutItem mfi)
        {
            mfi.Text = text;
        }
        else if (element is AppBarButton abb)
        {
            abb.Label = text;
        }
        else if (element is ContentControl cc)
        {
            // Only assign if content is string or null to preserve complex templates
            if (cc.Content is null or string)
            {
                cc.Content = text;
            }
        }
    }

    private static void ApplyHeaderToElement(FrameworkElement element)
    {
        string? headerUid = GetHeaderUid(element);
        if (string.IsNullOrWhiteSpace(headerUid)) return;

        string text = LocalizationService.Current.GetString(headerUid);
        if (string.IsNullOrEmpty(text) || text == headerUid) return;

        if (element is TextBox txt) txt.Header = text;
        else if (element is PasswordBox pb) pb.Header = text;
        else if (element is ComboBox cb) cb.Header = text;
        else if (element is AutoSuggestBox asb) asb.Header = text;
        else if (element is InfoBar ib) ib.Title = text;
    }

    private static void ApplyPlaceholderToElement(FrameworkElement element)
    {
        string? phUid = GetPlaceholderUid(element);
        if (string.IsNullOrWhiteSpace(phUid)) return;

        string text = LocalizationService.Current.GetString(phUid);
        if (string.IsNullOrEmpty(text) || text == phUid) return;

        if (element is TextBox txt) txt.PlaceholderText = text;
        else if (element is PasswordBox pb) pb.PlaceholderText = text;
        else if (element is ComboBox cb) cb.PlaceholderText = text;
        else if (element is AutoSuggestBox asb) asb.PlaceholderText = text;
    }

    private static void ApplyTooltipToElement(FrameworkElement element)
    {
        string? ttUid = GetTooltipUid(element);
        if (string.IsNullOrWhiteSpace(ttUid)) return;

        string text = LocalizationService.Current.GetString(ttUid);
        if (!string.IsNullOrEmpty(text) && text != ttUid)
        {
            ToolTipService.SetToolTip(element, text);
        }
    }

    /// <summary>
    /// Refreshes all currently registered and alive UI elements with the active language strings.
    /// </summary>
    public static void RefreshAll()
    {
        lock (RegisteredElements)
        {
            for (int i = RegisteredElements.Count - 1; i >= 0; i--)
            {
                if (RegisteredElements[i].TryGetTarget(out var element))
                {
                    try
                    {
                        if (element.DispatcherQueue != null)
                        {
                            element.DispatcherQueue.TryEnqueue(() => ApplyToElement(element));
                        }
                        else
                        {
                            ApplyToElement(element);
                        }
                    }
                    catch { }
                }
                else
                {
                    RegisteredElements.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Traverses the visual tree under the root object and applies localization to any element with a Loc attached property.
    /// </summary>
    public static void LocalizeTree(DependencyObject root)
    {
        if (root is FrameworkElement fe)
        {
            ApplyToElement(fe);
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            LocalizeTree(child);
        }
    }
}
