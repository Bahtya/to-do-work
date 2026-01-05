using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Todowork.Models;
using Todowork.Services;

namespace Todowork.ViewModels
{
    public sealed class MainViewModel : BaseNotify
    {
        private readonly TodoStore _store;
        private readonly DispatcherTimer _itemsViewRefreshTimer;
        private string _newText;
        private bool _isCompletedExpanded;
        private TodoItem _selectedItem;
        private bool _overlayShowBackground;
        private double _overlayLeftRatio;
        private double _overlayTopRatio;
        private double _overlayBackgroundOpacity = 0.67;
        private double _overlayTextOpacity = 1.0;
        private double _overlayTextFontSize = 16.0;
        private string _overlayTextColor = "#FFFFFFFF";

        public MainViewModel(TodoStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            _itemsViewRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            _itemsViewRefreshTimer.Tick += (s, e) =>
            {
                _itemsViewRefreshTimer.Stop();
                try { ItemsView.Refresh(); } catch { }
                try { CompletedItemsView.Refresh(); } catch { }
                try { CommandManager.InvalidateRequerySuggested(); } catch { }
            };

            var viewSource = new CollectionViewSource { Source = _store.Items };
            ItemsView = viewSource.View;
            ItemsView.Filter = FilterTodo;
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(TodoItem.IsPinned), ListSortDirection.Descending));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(TodoItem.CreatedAt), ListSortDirection.Descending));

            var completedViewSource = new CollectionViewSource { Source = _store.Items };
            CompletedItemsView = completedViewSource.View;
            CompletedItemsView.Filter = FilterCompleted;
            CompletedItemsView.SortDescriptions.Add(new SortDescription(nameof(TodoItem.CompletedAt), ListSortDirection.Descending));
            CompletedItemsView.SortDescriptions.Add(new SortDescription(nameof(TodoItem.CreatedAt), ListSortDirection.Descending));

            AddCommand = new RelayCommand(_ => Add(), _ => !string.IsNullOrWhiteSpace(NewText));
            DeleteCommand = new RelayCommand(p => Delete(p as TodoItem));
            TogglePinCommand = new RelayCommand(p => TogglePin(p as TodoItem));
            ClearCompletedCommand = new RelayCommand(_ => ClearCompleted(), _ => _store.Items.Any(i => i.IsCompleted));
            ToggleCompletedExpandedCommand = new RelayCommand(_ => IsCompletedExpanded = !IsCompletedExpanded);
            SetOverlayTextColorCommand = new RelayCommand(p =>
            {
                if (p == null) return;
                OverlayTextColor = p.ToString();
            });

            foreach (var item in _store.Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            _store.Items.CollectionChanged += Items_CollectionChanged;
        }

        private void ScheduleItemsViewRefresh(int delayMs)
        {
            if (delayMs <= 0)
            {
                try { ItemsView.Refresh(); } catch { }
                try { CompletedItemsView.Refresh(); } catch { }
                try { CommandManager.InvalidateRequerySuggested(); } catch { }
                return;
            }

            try
            {
                _itemsViewRefreshTimer.Stop();
                _itemsViewRefreshTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
                _itemsViewRefreshTimer.Start();
            }
            catch
            {
                try { ItemsView.Refresh(); } catch { }
            }
        }

        public ICollectionView ItemsView { get; }

        public ICollectionView CompletedItemsView { get; }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand TogglePinCommand { get; }
        public ICommand ClearCompletedCommand { get; }
        public ICommand ToggleCompletedExpandedCommand { get; }
        public ICommand SetOverlayTextColorCommand { get; }

        public int CompletedCount
        {
            get
            {
                try { return _store.Items.Count(i => i.IsCompleted); }
                catch { return 0; }
            }
        }

        public bool IsCompletedExpanded
        {
            get => _isCompletedExpanded;
            set
            {
                if (_isCompletedExpanded == value) return;
                _isCompletedExpanded = value;
                OnPropertyChanged();
            }
        }

        public string NewText
        {
            get => _newText;
            set
            {
                if (_newText == value) return;
                _newText = value;
                OnPropertyChanged();
            }
        }

        public double OverlayLeftRatio
        {
            get => _overlayLeftRatio;
            set
            {
                if (Math.Abs(_overlayLeftRatio - value) < 0.0001) return;
                _overlayLeftRatio = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayLeftRatio(_overlayLeftRatio);
                }
                catch { }
            }
        }

        public void SetOverlayLeftRatioSilently(double value)
        {
            _overlayLeftRatio = value;
            OnPropertyChanged(nameof(OverlayLeftRatio));
        }

        public double OverlayTopRatio
        {
            get => _overlayTopRatio;
            set
            {
                if (Math.Abs(_overlayTopRatio - value) < 0.0001) return;
                _overlayTopRatio = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayTopRatio(_overlayTopRatio);
                }
                catch { }
            }
        }

        public void SetOverlayTopRatioSilently(double value)
        {
            _overlayTopRatio = value;
            OnPropertyChanged(nameof(OverlayTopRatio));
        }

        public bool OverlayShowBackground
        {
            get => _overlayShowBackground;
            set
            {
                if (_overlayShowBackground == value) return;
                _overlayShowBackground = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayShowBackground(_overlayShowBackground);
                }
                catch { }
            }
        }

        public double OverlayBackgroundOpacity
        {
            get => _overlayBackgroundOpacity;
            set
            {
                if (Math.Abs(_overlayBackgroundOpacity - value) < 0.0001) return;
                _overlayBackgroundOpacity = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayBackgroundOpacity(_overlayBackgroundOpacity);
                }
                catch { }
            }
        }

        public double OverlayTextOpacity
        {
            get => _overlayTextOpacity;
            set
            {
                if (Math.Abs(_overlayTextOpacity - value) < 0.0001) return;
                _overlayTextOpacity = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayTextOpacity(_overlayTextOpacity);
                }
                catch { }
            }
        }

        public double OverlayTextFontSize
        {
            get => _overlayTextFontSize;
            set
            {
                if (Math.Abs(_overlayTextFontSize - value) < 0.0001) return;
                _overlayTextFontSize = value;
                OnPropertyChanged();

                try
                {
                    var app = Application.Current as Todowork.App;
                    app?.SetOverlayTextFontSize(_overlayTextFontSize);
                }
                catch { }
            }
        }

        public string OverlayTextColor
        {
            get => _overlayTextColor;
            set
            {
                if (_overlayTextColor == value) return;
                _overlayTextColor = value;
                OnPropertyChanged();

                try
                {
                    var brush = CreateBrushFromString(_overlayTextColor);
                    if (brush != null)
                    {
                        var app = Application.Current as Todowork.App;
                        app?.SetOverlayTextBrush(brush);
                    }
                }
                catch { }
            }
        }

        private static Brush CreateBrushFromString(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            try
            {
                var obj = ColorConverter.ConvertFromString(value);
                if (obj == null) return null;
                var color = (Color)obj;
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        public TodoItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        private void NudgeOverlay(double dx)
        {
            try
            {
                var app = Application.Current as Todowork.App;
                app?.NudgeOverlayLeft(dx);
            }
            catch { }
        }

        private bool FilterTodo(object obj)
        {
            if (!(obj is TodoItem item)) return false;
            return !item.IsCompleted;
        }

        private bool FilterCompleted(object obj)
        {
            if (!(obj is TodoItem item)) return false;
            return item.IsCompleted;
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (TodoItem item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (TodoItem item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(CompletedCount));
            ScheduleItemsViewRefresh(0);
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TodoItem.IsPinned))
            {
                ScheduleItemsViewRefresh(0);
                return;
            }

            if (e.PropertyName == nameof(TodoItem.IsCompleted))
            {
                OnPropertyChanged(nameof(CompletedCount));
                ScheduleItemsViewRefresh(220);
                return;
            }
        }

        private void Add()
        {
            var text = NewText;
            var item = _store.Add(text);
            if (item == null) return;

            NewText = string.Empty;
            ScheduleItemsViewRefresh(0);
        }

        private void Delete(TodoItem item)
        {
            if (item == null) return;
            _store.Remove(item);
            ScheduleItemsViewRefresh(0);
        }

        private void ClearCompleted()
        {
            try
            {
                var completed = _store.Items.Where(i => i.IsCompleted).ToList();
                foreach (var item in completed)
                {
                    _store.Remove(item);
                }
            }
            catch { }

            ScheduleItemsViewRefresh(0);
        }

        private void TogglePin(TodoItem item)
        {
            if (item == null) return;
            if (item.IsCompleted) return;
            item.IsPinned = !item.IsPinned;
            ScheduleItemsViewRefresh(0);
        }
    }
}
