using System;
using System.Runtime.Serialization;
using Todowork.ViewModels;

namespace Todowork.Models
{
    [DataContract]
    public sealed class TodoItem : BaseNotify
    {
        private string _text;
        private bool _isCompleted;
        private bool _isPinned;
        private DateTime? _completedAt;

        private bool _isEditing;
        private string _editText;

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value;
                OnPropertyChanged();
            }
        }

        [DataMember]
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted == value) return;
                _isCompleted = value;

                if (_isCompleted)
                {
                    if (_isPinned)
                    {
                        _isPinned = false;
                        OnPropertyChanged(nameof(IsPinned));
                    }
                    CompletedAt = DateTime.Now;
                }
                else
                {
                    CompletedAt = null;
                }

                OnPropertyChanged();
            }
        }

        [DataMember]
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned == value) return;
                _isPinned = value;
                if (_isPinned && _isCompleted)
                {
                    _isPinned = false;
                }
                OnPropertyChanged();
            }
        }

        [DataMember]
        public DateTime CreatedAt { get; set; }

        [DataMember]
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set
            {
                if (_completedAt == value) return;
                _completedAt = value;
                OnPropertyChanged();
            }
        }

        [IgnoreDataMember]
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing == value) return;
                _isEditing = value;
                OnPropertyChanged();
            }
        }

        [IgnoreDataMember]
        public string EditText
        {
            get => _editText;
            set
            {
                if (_editText == value) return;
                _editText = value;
                OnPropertyChanged();
            }
        }
    }
}
