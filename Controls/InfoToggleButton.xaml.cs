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

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    private void CheckedChanged(CheckBox checkBox, bool value)
    {
        if (_isRecursion) return;

        _isRecursion = true;
        if (checkBox is { Tag: InfoCategory category })
        {
            ViewModel.SetInfoCategoryVisibility(category, value);
            _allCheckBox.IsChecked = _checkBoxes.Any(checkBox => checkBox.IsChecked == !value) ? null : value;
        }
        else
        {
            ViewModel.SetInfoCategoryVisibility(null, value);
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
        secondaryButton.Padding = new Thickness(0, 0, 5, 0);
    }
}
