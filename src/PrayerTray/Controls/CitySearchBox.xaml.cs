using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrayerTray.Models;

namespace PrayerTray.Controls;

public partial class CitySearchBox : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(CitySearchBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSearchTextChanged));

    public static readonly DependencyProperty ResultsProperty =
        DependencyProperty.Register(
            nameof(Results),
            typeof(IEnumerable),
            typeof(CitySearchBox),
            new PropertyMetadata(null, OnResultsChanged));

    public static readonly DependencyProperty SelectedCityProperty =
        DependencyProperty.Register(
            nameof(SelectedCity),
            typeof(CityOption),
            typeof(CitySearchBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedCityChanged));

    public static readonly DependencyProperty IsSearchingProperty =
        DependencyProperty.Register(
            nameof(IsSearching),
            typeof(bool),
            typeof(CitySearchBox),
            new PropertyMetadata(false, OnIsSearchingChanged));

    private INotifyCollectionChanged? _resultsSubscription;
    private bool _syncingSelection;

    public CitySearchBox()
    {
        InitializeComponent();
        IsEnabledChanged += (_, _) =>
        {
            if (IsEnabled)
            {
                Dispatcher.BeginInvoke(FocusSearch, DispatcherPriority.Input);
            }
        };
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public IEnumerable Results
    {
        get => (IEnumerable)GetValue(ResultsProperty);
        set => SetValue(ResultsProperty, value);
    }

    public CityOption? SelectedCity
    {
        get => (CityOption?)GetValue(SelectedCityProperty);
        set => SetValue(SelectedCityProperty, value);
    }

    public bool IsSearching
    {
        get => (bool)GetValue(IsSearchingProperty);
        set => SetValue(IsSearchingProperty, value);
    }

    public void FocusSearch()
    {
        CityCombo.Focus();
        if (CityCombo.Template?.FindName("PART_EditableTextBox", CityCombo) is System.Windows.Controls.TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CitySearchBox box)
        {
            box.UpdateDropdownState();
        }
    }

    private static void OnResultsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CitySearchBox box)
        {
            return;
        }

        box.UnsubscribeFromResults(e.OldValue as IEnumerable);
        box.SubscribeToResults(e.NewValue as IEnumerable);
        box.UpdateDropdownState();
    }

    private static void OnIsSearchingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CitySearchBox box)
        {
            box.UpdateDropdownState();
        }
    }

    private static void OnSelectedCityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CitySearchBox box || box._syncingSelection)
        {
            return;
        }

        if (e.NewValue is CityOption city)
        {
            box._syncingSelection = true;
            box.SearchText = city.DisplayName;
            box._syncingSelection = false;
            box.CityCombo.IsDropDownOpen = false;
        }
    }

    private void SubscribeToResults(IEnumerable? results)
    {
        if (results is INotifyCollectionChanged collection)
        {
            _resultsSubscription = collection;
            collection.CollectionChanged += OnResultsCollectionChanged;
        }
    }

    private void UnsubscribeFromResults(IEnumerable? results)
    {
        if (results is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnResultsCollectionChanged;
        }

        if (ReferenceEquals(_resultsSubscription, results))
        {
            _resultsSubscription = null;
        }
    }

    private void OnResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateDropdownState();

    private void UpdateDropdownState()
    {
        var queryLength = SearchText.Trim().Length;
        if (queryLength < 2)
        {
            CityCombo.IsDropDownOpen = false;
            return;
        }

        var resultCount = Results is ICollection collection ? collection.Count : 0;
        CityCombo.IsDropDownOpen = resultCount > 0 || IsSearching;
    }
}
