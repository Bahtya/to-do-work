using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;
using Todowork.Models;
using Todowork.Services;

namespace Todowork.ViewModels
{
    public sealed class OverlayViewModel : BaseNotify
    {
        private readonly TodoStore _store;

        public OverlayViewModel(TodoStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            var viewSource = new CollectionViewSource { Source = _store.Items };
            PinnedView = viewSource.View;
            PinnedView.Filter = Filter;
            PinnedView.SortDescriptions.Add(new SortDescription(nameof(TodoItem.CreatedAt), ListSortDirection.Descending));

            CompleteCommand = new RelayCommand(p => Complete(p as TodoItem));
            UnpinCommand = new RelayCommand(p => Unpin(p as TodoItem));

            foreach (var item in _store.Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            _store.Items.CollectionChanged += Items_CollectionChanged;

            PinnedView.Refresh();
        }

        public ICollectionView PinnedView { get; }

        public ICommand CompleteCommand { get; }
        public ICommand UnpinCommand { get; }

        private bool Filter(object obj)
        {
            if (!(obj is TodoItem item)) return false;
            return item.IsPinned && !item.IsCompleted;
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

            PinnedView.Refresh();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TodoItem.IsPinned) || e.PropertyName == nameof(TodoItem.IsCompleted))
            {
                try
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { PinnedView.Refresh(); } catch { }
                        }));
                    }
                    else
                    {
                        PinnedView.Refresh();
                    }
                }
                catch
                {
                    try { PinnedView.Refresh(); } catch { }
                }
            }
        }

        private void Complete(TodoItem item)
        {
            if (item == null) return;
            item.IsCompleted = true;
            PinnedView.Refresh();
        }

        private void Unpin(TodoItem item)
        {
            if (item == null) return;
            item.IsPinned = false;
            PinnedView.Refresh();
        }
    }
}
