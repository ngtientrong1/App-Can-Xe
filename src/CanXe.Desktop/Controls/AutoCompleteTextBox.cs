using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Controls;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public class AutoCompleteTextBox : Control
{
    private static readonly HashSet<AutoCompleteTextBox> ActiveInstances = [];

    private TextBox? _textBox;
    private ListBox? _listBox;
    private bool _isCommitting;
    private bool _moveFocusAfterCommit;

    static AutoCompleteTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(typeof(AutoCompleteTextBox)));
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AutoCompleteTextBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SuppressDropDownProperty =
        DependencyProperty.Register(nameof(SuppressDropDown), typeof(bool), typeof(AutoCompleteTextBox),
            new PropertyMetadata(false, static (d, e) =>
            {
                if (d is AutoCompleteTextBox box && e.NewValue is true)
                    box.ClosePopup();
            }));

    public bool SuppressDropDown
    {
        get => (bool)GetValue(SuppressDropDownProperty);
        set => SetValue(SuppressDropDownProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HighlightedIndexProperty =
        DependencyProperty.Register(nameof(HighlightedIndex), typeof(int), typeof(AutoCompleteTextBox),
            new PropertyMetadata(-1, OnHighlightedIndexChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public int HighlightedIndex
    {
        get => (int)GetValue(HighlightedIndexProperty);
        set => SetValue(HighlightedIndexProperty, value);
    }

    public event EventHandler? TextChangedByUser;
    public event EventHandler? ItemCommitted;

    public static void CloseAllDropDowns()
    {
        foreach (var instance in ActiveInstances.ToArray())
            instance.ClosePopup();
    }

    public void FocusInput()
    {
        _textBox?.Focus();
        _textBox?.SelectAll();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ActiveInstances.Add(this);

        Unloaded += (_, _) => ActiveInstances.Remove(this);

        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        _listBox = GetTemplateChild("PART_ListBox") as ListBox;

        if (_textBox is not null)
        {
            _textBox.TextChanged += (_, _) =>
            {
                if (_isCommitting)
                    return;

                Text = _textBox.Text;
                TextChangedByUser?.Invoke(this, EventArgs.Empty);
                SelectedItem = null;
                HighlightedIndex = -1;
                UpdatePopupState();
            };
            _textBox.GotFocus += (_, _) => ClosePopup();
            _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            _textBox.LostFocus += (_, e) =>
            {
                if (_listBox?.IsKeyboardFocusWithin == true)
                {
                    e.Handled = true;
                    return;
                }

                Dispatcher.BeginInvoke(ClosePopup);
            };
        }

        if (_listBox is not null)
        {
            _listBox.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(_listBox, source) is ListBoxItem { Content: var item })
                {
                    CommitItem(item, moveFocus: true);
                    e.Handled = true;
                }
            };
        }

        var window = Window.GetWindow(this);
        if (window is not null)
        {
            window.Deactivated += OnOwnerWindowDeactivated;
            window.StateChanged += OnOwnerWindowStateChanged;
            Unloaded += (_, _) =>
            {
                window.Deactivated -= OnOwnerWindowDeactivated;
                window.StateChanged -= OnOwnerWindowStateChanged;
            };
        }
    }

    private void OnOwnerWindowDeactivated(object? sender, EventArgs e) => ClosePopup();

    private void OnOwnerWindowStateChanged(object? sender, EventArgs e)
    {
        if (Window.GetWindow(this)?.WindowState == WindowState.Minimized)
            ClosePopup();
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var items = GetItems();
        var hasPopup = IsDropDownOpen && items.Count > 0;

        switch (e.Key)
        {
            case Key.Down when hasPopup:
                HighlightedIndex = AutoCompleteSelectionLogic.MoveHighlight(HighlightedIndex, items.Count, 1);
                SyncListSelection();
                e.Handled = true;
                break;

            case Key.Up when hasPopup:
                HighlightedIndex = AutoCompleteSelectionLogic.MoveHighlight(HighlightedIndex, items.Count, -1);
                SyncListSelection();
                e.Handled = true;
                break;

            case Key.Enter:
                _moveFocusAfterCommit = true;
                if (hasPopup && HighlightedIndex >= 0)
                    CommitHighlighted();
                else
                    CommitText(Text.Trim(), SelectedItem, moveFocus: true);
                e.Handled = true;
                break;

            case Key.Tab:
                _moveFocusAfterCommit = true;
                if (hasPopup && HighlightedIndex >= 0)
                    CommitHighlighted();
                else
                    CommitText(Text.Trim(), SelectedItem, moveFocus: !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift));
                break;

            case Key.Escape when IsDropDownOpen:
                ClosePopup();
                e.Handled = true;
                break;
        }
    }

    private void CommitHighlighted()
    {
        var items = GetItems();
        if (HighlightedIndex >= 0 && HighlightedIndex < items.Count)
        {
            CommitItem(items[HighlightedIndex], _moveFocusAfterCommit);
            return;
        }

        CommitText(Text.Trim(), SelectedItem, _moveFocusAfterCommit);
    }

    private void CommitItem(object item, bool moveFocus)
    {
        if (item is AutocompleteSuggestionItem suggestion)
        {
            if (suggestion.IsNewEntryOption)
                CommitText(Text.Trim(), suggestion, moveFocus);
            else
                CommitText(suggestion.PrimaryText, suggestion, moveFocus);
            return;
        }

        CommitText(item.ToString() ?? string.Empty, item, moveFocus);
    }

    private void CommitText(string text, object? selectedItem, bool moveFocus)
    {
        _isCommitting = true;
        try
        {
            Text = text;
            SelectedItem = selectedItem;
            if (_textBox is not null)
                _textBox.Text = text;
            ClosePopup();
            ItemCommitted?.Invoke(this, EventArgs.Empty);
            if (moveFocus)
                MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
        finally
        {
            _isCommitting = false;
            _moveFocusAfterCommit = false;
        }
    }

    private void ClosePopup()
    {
        IsDropDownOpen = false;
        HighlightedIndex = -1;
    }

    private void SyncListSelection()
    {
        if (_listBox is null || HighlightedIndex < 0)
            return;

        _listBox.SelectedIndex = HighlightedIndex;
        _listBox.ScrollIntoView(_listBox.SelectedItem);
    }

    private List<object> GetItems()
    {
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        var limit = AutocompleteDropDownPolicy.LimitItemCount(items.Count);
        return items.Take(limit).ToList();
    }

    private bool IsOwnerWindowActive()
    {
        var window = Window.GetWindow(this);
        return window is { IsActive: true, WindowState: not WindowState.Minimized };
    }

    private void UpdatePopupState(bool selectFirst = false)
    {
        if (!IsVisible || !IsEnabled || !IsOwnerWindowActive())
        {
            ClosePopup();
            return;
        }

        var items = GetItems();
        var isFocused = _textBox?.IsFocused == true;
        var shouldOpen = AutocompleteDropDownPolicy.ShouldOpen(
            isFocused,
            Text,
            items.Count,
            SuppressDropDown,
            IsOwnerWindowActive());

        IsDropDownOpen = shouldOpen;
        if (shouldOpen)
        {
            foreach (var other in ActiveInstances.Where(x => x != this))
                other.ClosePopup();
            if (HighlightedIndex < 0 && items.Count > 0)
                HighlightedIndex = 0;
        }
        else if (!shouldOpen)
            HighlightedIndex = -1;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box && box._textBox is not null && box._textBox.Text != (string?)e.NewValue)
            box._textBox.Text = (string?)e.NewValue ?? string.Empty;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box)
            box.UpdatePopupState();
    }

    private static void OnHighlightedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box)
            box.SyncListSelection();
    }
}
