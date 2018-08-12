using System;
using System.ComponentModel;

namespace ObservableContext
{
    public interface IAnonymousValue : INotifyPropertyChanged, IDisposable
    {
        object Value { get; }
    }

    public interface IAnonymousValue<out T> : INotifyPropertyChanged, IDisposable
    {
        T Value { get; }
    }
}