using System;
using System.Linq;
using CommunityToolkit.WinUI;
using cycloid.Info;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class InfoToggleButton : UserControl
{
    private readonly CheckBox _allCheckBox;
    private readonly CheckBox[] _checkBoxes;
    private bool _isRecursion;

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(InfoToggleButton), new PropertyMetadata(false));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(InfoToggleButton), new PropertyMetadata(false));

    public Symbol Symbol
    {
        get => (Symbol)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof(Symbol), typeof(Symbol), typeof(InfoToggleButton), new PropertyMetadata(null));

    public int VisibleCount
    {
        get => (int)GetValue(VisibleCountProperty);
        set => SetValue(VisibleCountProperty, value);
    }

    public static readonly DependencyProperty VisibleCountProperty = DependencyProperty.Register(nameof(VisibleCount), typeof(int), typeof(InfoToggleButton), new PropertyMetadata(0));

    public int TotalCount
    {
        get => (int)GetValue(TotalCountProperty);
        set => SetValue(TotalCountProperty, value);
    }

    public static readonly DependencyProperty TotalCountProperty = DependencyProperty.Register(nameof(TotalCount), typeof(int), typeof(InfoToggleButton), new PropertyMetadata(0));

    public event Action<InfoCategory, bool> CategoryChanged;

    public InfoToggleButton()
    {
        InitializeComponent();

        Loaded += InfoToggleButton_Loaded;

        foreach (CheckBox checkBox in CheckBoxContainer.Children)
        {
            checkBox.IsChecked = true;
            checkBox.Checked += CheckBox_Checked;
            checkBox.Unchecked += CheckBox_Unchecked;
        }

        _allCheckBox = (CheckBox)CheckBoxContainer.Children.First();
        _checkBoxes = CheckBoxContainer.Children.Skip(1).Cast<CheckBox>().ToArray();
    }

    private void CheckedChanged(CheckBox checkBox, bool value)
    {
        if (_isRecursion) return;

        _isRecursion = true;
        if (checkBox is { Tag: InfoCategory category })
        {
            CategoryChanged?.Invoke(category, value);
            _allCheckBox.IsChecked = _checkBoxes.Any(checkBox => checkBox.IsChecked == !value) ? null : value;
        }
        else
        {
            CategoryChanged?.Invoke(null, value);
            Array.ForEach(_checkBoxes, checkBox => checkBox.IsChecked = value);
        }
        _isRecursion = false;
    }

    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        CheckedChanged((CheckBox)sender, false);
    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        CheckedChanged((CheckBox)sender, true);
    }

    private void InfoToggleButton_Loaded(object _1, RoutedEventArgs _2)
    {
        // Workaround: Center the chevron
        Button secondaryButton = (Button)Content.FindDescendant("SecondaryButton");
        secondaryButton.Padding = new Thickness(0, 0, 2.5, 0);
    }
}
