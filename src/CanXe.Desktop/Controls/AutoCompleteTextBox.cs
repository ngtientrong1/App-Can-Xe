using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CanXe.Desktop.Controls;

public class AutoCompleteTextBox : Control
{
    private TextBox? _textBox;
    private Popup? _popup;
    private ListBox? _listBox;

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
            new PropertyMetadata(null, (_, _) => { }));

    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.Register(nameof(Watermark), typeof(string), typeof(AutoCompleteTextBox),
            new PropertyMetadata(string.Empty));

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

    public string Watermark
    {
        get => (string)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public event EventHandler? TextChangedByUser;

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
                Text = _textBox.Text;
                TextChangedByUser?.Invoke(this, EventArgs.Empty);
                UpdatePopupState();
            };
            _textBox.GotFocus += (_, _) => UpdatePopupState();
            _textBox.LostFocus += (_, _) => Dispatcher.BeginInvoke(() => _popup?.SetCurrentValue(Popup.IsOpenProperty, false));
        }

        if (_listBox is not null)
        {
            _listBox.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (_listBox.SelectedItem is string selected)
                {
                    Text = selected;
                    if (_textBox is not null)
                        _textBox.Text = selected;
                    _popup?.SetCurrentValue(Popup.IsOpenProperty, false);
                    e.Handled = true;
                }
            };
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoCompleteTextBox box && box._textBox is not null && box._textBox.Text != (string?)e.NewValue)
            box._textBox.Text = (string?)e.NewValue ?? string.Empty;
    }

    private void UpdatePopupState()
    {
        if (_popup is null || _listBox is null)
            return;

        var hasItems = ItemsSource?.Cast<object>().Any() == true;
        var isFocused = _textBox?.IsFocused == true;
        _popup.IsOpen = isFocused && hasItems && !string.IsNullOrWhiteSpace(Text);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ItemsSourceProperty)
            UpdatePopupState();
    }
}
