using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;

namespace ObservableContext
{
    public sealed class Context : DynamicObject, IContext, INotifyPropertyChanged, IDisposable
    {
        private class DependencyTracker : IContext
        {
            private readonly Context self;

            public readonly HashSet<string> Dependencies = new HashSet<string>();

            public DependencyTracker(Context self)
            {
                this.self = self;
            }

            public T Get<T>(string key)
            {
                Dependencies.Add(key);
                return self.Get<T>(key);
            }
        }

        private abstract class AnonymousValueBase : INotifyPropertyChanged, IDisposable
        {
            protected bool Disposed;
            protected readonly Context Self;

            protected AnonymousValueBase(Context self)
            {
                Self = self;
            }

            ~AnonymousValueBase() => Dispose(false);

            public HashSet<string> Dependencies = new HashSet<string>();

            public event PropertyChangedEventHandler PropertyChanged;

            public void RaiseValueChanged()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
            }

            public void Dispose() => Dispose(true);

            private void Dispose(bool disposing)
            {
                if (Disposed)
                {
                    return;
                }

                Self?.anonymousValues?.Remove(this);
                Disposed = true;
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        private class AnonymousValue<T> : AnonymousValueBase, IAnonymousValue<T>
        {
            private readonly IValue<T> value;

            public AnonymousValue(Context self, IValue<T> value) : base(self)
            {
                this.value = value;
            }

            public T Value
            {
                get
                {
                    if (Disposed)
                    {
                        throw new ObjectDisposedException(nameof(IAnonymousValue<T>));
                    }

                    var tracker = new DependencyTracker(Self);
                    var result = value.Get(tracker);
                    Dependencies = tracker.Dependencies;
                    return result;
                }
            }
        }

        private readonly Context parentContext;
        private readonly Dictionary<string, IValue<object>> ownValues = new Dictionary<string, IValue<object>>();
        private readonly Dictionary<string, HashSet<string>> dependencyMap = new Dictionary<string, HashSet<string>>();
        private readonly WeakList<AnonymousValueBase> anonymousValues = new WeakList<AnonymousValueBase>();
        private readonly HashSet<Context> children = new HashSet<Context>();

        public Context(Context parentContext)
        {
            this.parentContext = parentContext;
            parentContext?.children.Add(this);
        }

        public IEnumerable<Context> Children => children;

        public Context Parent => parentContext;

        public void AttachChild(Context child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            children.Add(child);
        }

        public void Detach()
        {
            parentContext?.children.Remove(this);
        }

        public void Dispose()
        {
            Detach();
        }

        public T Get<T>(string key)
        {
            var valueProvider = TryFindValue(key.Replace('/', '.'));
            if (valueProvider == null)
            {
                throw new KeyNotFoundException();
            }

            if (!(valueProvider is IValue<T> tValueProvider))
            {
                throw new InvalidOperationException($"Value '{key}' is not compatible with the requested type.");
            }

            var tracker = new DependencyTracker(this);
            var result = tValueProvider.Get(tracker);
            dependencyMap[key] = tracker.Dependencies;
            return result;
        }

        public T Get<T>(IValue<T> value)
        {
            return value.Get(this);
        }

        public IAnonymousValue<T> Subscribe<T>(IValue<T> value)
        {
            var anonymousValue = new AnonymousValue<T>(this, value);
            anonymousValues.Add(anonymousValue);
            return anonymousValue;
        }

        public void Set(string key, IValue<object> value)
        {
            ownValues[key] = value;
            Refresh(key);
        }

        public bool Has(string key)
        {
            return ownValues.ContainsKey(key) || parentContext != null && parentContext.Has(key);
        }

        public bool HasOwn(string key)
        {
            return ownValues.ContainsKey(key);
        }

        public bool DeleteOwn(string key)
        {
            if (ownValues.ContainsKey(key))
            {
                if (dependencyMap.Any(pair => pair.Value.Contains(key)))
                {
                    throw new InvalidOperationException("Cannot delete while other values depend on this entry.");
                }

                dependencyMap.Remove(key);
                Refresh(key);
                return true;
            }

            return false;
        }

        public void Refresh(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            RefreshInternal(key, new HashSet<string>(), new HashSet<AnonymousValueBase>());
        }

        public IValue<object> TryFindValue(string key)
        {
            return ownValues.TryGetValue(key, out var value)
                ? value
                : parentContext?.TryFindValue(key);
        }

        private void RefreshInternal(string key, HashSet<string> raised, HashSet<AnonymousValueBase> raisedAnonymous)
        {
            if (raised.Contains(key))
            {
                return;
            }

            raised.Add(key);
            RaiseChanged(key);
            foreach (var pair in dependencyMap)
            {
                if (pair.Value.Contains(key))
                {
                    RefreshInternal(pair.Key, raised, raisedAnonymous);
                }
            }

            foreach (var value in anonymousValues)
            {
                if (value != null && !raisedAnonymous.Contains(value))
                {
                    if (value.Dependencies.Contains(key))
                    {
                        value.RaiseValueChanged();
                        raisedAnonymous.Add(value);
                    }
                }
            }

            foreach (var child in children)
            {
                child.Refresh(key);
            }
        }

        public IValue<T> TryFindValue<T>(string key)
        {
            return TryFindValue(key) as IValue<T>;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var key = binder.Name.Replace('/', '.');
            var valueProvider = TryFindValue(key);
            if (valueProvider == null)
            {
                result = null;
                return false;
            }

            var tracker = new DependencyTracker(this);
            var value = valueProvider.Get(tracker);
            dependencyMap[key] = tracker.Dependencies;
            result = value;
            return true;
        }

        private void RaiseChanged(string key)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key.Replace('.', '/')));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
