using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Todowork.Models;

namespace Todowork.Services
{
    public sealed class TodoStore : IDisposable
    {
        private readonly TodoRepository _repository;
        private readonly DispatcherTimer _saveDebounceTimer;
        private bool _isDisposed;

        public ObservableCollection<TodoItem> Items { get; } = new ObservableCollection<TodoItem>();

        public TodoStore(TodoRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            Items.CollectionChanged += Items_CollectionChanged;

            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _saveDebounceTimer.Stop();
                _saveDebounceTimer.Tick -= SaveDebounceTimer_Tick;
            }
            catch { }

            try
            {
                Items.CollectionChanged -= Items_CollectionChanged;
            }
            catch { }

            try
            {
                foreach (var item in Items)
                {
                    UnhookItem(item);
                }
            }
            catch { }
        }

        public void Load()
        {
            Items.CollectionChanged -= Items_CollectionChanged;
            try
            {
                Items.Clear();
                foreach (var item in _repository.Load())
                {
                    if (item.Id == Guid.Empty)
                    {
                        item.Id = Guid.NewGuid();
                    }
                    if (item.CreatedAt == default)
                    {
                        item.CreatedAt = DateTime.Now;
                    }

                    HookItem(item);
                    Items.Add(item);
                }
            }
            finally
            {
                Items.CollectionChanged += Items_CollectionChanged;
            }
        }

        public TodoItem Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var item = new TodoItem
            {
                Id = Guid.NewGuid(),
                Text = text.Trim(),
                CreatedAt = DateTime.Now,
                IsCompleted = false,
                IsPinned = false,
                CompletedAt = null
            };

            HookItem(item);
            Items.Insert(0, item);
            RequestSave();
            return item;
        }

        public void Remove(TodoItem item)
        {
            if (item == null) return;
            UnhookItem(item);
            Items.Remove(item);
            RequestSave();
        }

        public void RequestSave()
        {
            if (_isDisposed) return;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        public void Save()
        {
            _repository.Save(Items.ToList());
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (TodoItem item in e.OldItems)
                {
                    UnhookItem(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (TodoItem item in e.NewItems)
                {
                    HookItem(item);
                }
            }

            RequestSave();
        }

        private void HookItem(TodoItem item)
        {
            if (item == null) return;
            item.PropertyChanged += Item_PropertyChanged;
        }

        private void UnhookItem(TodoItem item)
        {
            if (item == null) return;
            item.PropertyChanged -= Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RequestSave();
        }

        private void SaveDebounceTimer_Tick(object sender, EventArgs e)
        {
            if (_isDisposed) return;
            _saveDebounceTimer.Stop();
            Save();
        }
    }
}
