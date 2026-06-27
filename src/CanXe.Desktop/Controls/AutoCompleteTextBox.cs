using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Controls;

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
    private TextBox? _textBox;
    private Popup? _popup;
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
        DependencyProperty.Register(nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(AutoCompleteTextBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty HighlightedIndexProperty =
        DependencyProperty.Register(nameof(HighlightedIndex), typeof(int), typeof(AutoCompleteTextBox),
            new PropertyMetadata(-1, OnHighlightedIndexChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public System.Collections.IEnumerable? ItemsSource
    {
        get => (System.Collections.IEnumerable?)GetValue(ItemsSourceProperty);
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

    public void FocusInput()
    {
        _textBox?.Focus();
        _textBox?.SelectAll();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        _popup = GetTemplateChild("PART_Popup") as Popup;
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
            _textBox.GotFocus += (_, _) => UpdatePopupState(true);
            _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            _textBox.LostFocus += (_, e) =>
            {
                if (_popup?.IsOpen == true && _listBox?.IsKeyboardFocusWithin == true)
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

            case Key.Enter when hasPopup:
                _moveFocusAfterCommit = true;
                CommitHighlighted();
                e.Handled = true;
                break;

            case Key.Tab when hasPopup && HighlightedIndex >= 0:
                _moveFocusAfterCommit = true;
                CommitHighlighted();
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

        ClosePopup();
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

        var text = item.ToString() ?? string.Empty;
        CommitText(text, item, moveFocus);
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

    private List<object> GetItems() =>
        ItemsSource?.Cast<object>().ToList() ?? [];

    private void UpdatePopupState(bool selectFirst = false)
    {
        var items = GetItems();
        var isFocused = _textBox?.IsFocused == true;
        var shouldOpen = isFocused && items.Count > 0 &&
                         (selectFirst || !string.IsNullOrWhiteSpace(Text));

        IsDropDownOpen = shouldOpen;
        if (shouldOpen && items.Count > 0)
            HighlightedIndex = 0;
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

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box && box._popup is not null)
            box._popup.IsOpen = (bool)e.NewValue;
    }

    private static void OnHighlightedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box)
            box.SyncListSelection();
    }
}
