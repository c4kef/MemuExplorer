using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBot.Views.Dialogs
{
    public class MessageView : BaseView, INotifyPropertyChanged
    {
        public MessageView(string title, string message, bool isNoVisible)
        {
            Title = title;
            Content = message;
            IsNoVisible = isNoVisible;
        }

        private string _content;

        public string Content
        {
            get => _content;
            set { SetProperty(ref _content, value); }
        }

        private string _title;

        public string Title
        {
            get => _title;
            set { SetProperty(ref _title, value); }
        }

        private bool _isNoVisible;

        public bool IsNoVisible
        {
            get => _isNoVisible;
            set { SetProperty(ref _isNoVisible, value); }
        }
    }
}
