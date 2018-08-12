using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;

namespace ObservableContext
{
    public class Context : DynamicObject, IContext, INotifyPropertyChanged, IDisposable
    {
        private class DependencyTracker : IContext
        {
            private readonly Context self;
            private readonly string valueKey;

            public readonly HashSet<string> Dependencies = new HashSet<string>();

            public DependencyTracker(Context self, string valueKey)
            {
                this.self = self;
                this.valueKey = valueKey;
            }

            public bool TryGet<T>(string key, out T result)
            {
                if (valueKey != null && key == valueKey)
                {
                    result = default;
                    return false;
                }

                Dependencies.Add(key);
                return self.TryGet(key, out result);
            }

            public T Get<T>(string key)
            {
                key = key.Replace('/', '.');
                if (valueKey != null && key == valueKey)
                {
                    throw new InvalidOperationException("Recursive calls on the same property are not allowed.");
                }

                Dependencies.Add(key);
                return self.Get<T>(key);
            }
        }

        private abstract class AnonymousValueBase : INotifyPropertyChanged, IDisposable
        {
            protected readonly Context Self;
            protected bool Disposed;
            protected bool Dirty = true;

            protected AnonymousValueBase(Context self)
            {
                Self = self;
            }

            ~AnonymousValueBase() => Dispose(false);

            public HashSet<string> Dependencies = new HashSet<string>();

            public event PropertyChangedEventHandler PropertyChanged;

            public void RaiseValueChanged()
            {
                Dirty = true;
                ClearCache();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
            }

            public void Dispose() => Dispose(true);

            private void Dispose(bool disposing)
            {
                if (Disposed)
                {
                    return;
                }

                Disposed = true;
                if (disposing)
                {
                    Self?.anonymousValues?.Remove(this);
                    GC.SuppressFinalize(this);
                }
                else
                {
                    var values = Self?.anonymousValues;
                    if (values == null)
                    {
                        return;
                    }

                    if (!values.Remove(this))
                    {
                        values.Purge();
                    }
                }
            }

            protected abstract void ClearCache();
        }

        private class AnonymousValue<T> : AnonymousValueBase, IAnonymousValue<T>, IAnonymousValue
        {
            private readonly IValue<T> value;
            private T result;

            public AnonymousValue(Context self, IValue value) : base(self)
            {
                this.value = value.Coerce<T>();
            }

            public T Value
            {
                get
                {
                    if (Disposed)
                    {
                        throw new ObjectDisposedException(nameof(IAnonymousValue<T>));
                    }

                    if (!Dirty)
                    {
                        return result;
                    }

                    var tracker = new DependencyTracker(Self, null);
                    result = value.Get(tracker);
                    Dependencies = tracker.Dependencies;
                    Dirty = false;
                    return result;
                }
            }

            object IAnonymousValue.Value => Value;

            protected override void ClearCache()
            {
                result = default;
            }
        }

        private struct ValuePair
        {
            public ValuePair(IValue value, bool enumerable)
            {
                Value = value;
                Enumerable = enumerable;
            }

            public readonly IValue Value;
            public readonly bool Enumerable;
        }

        private Context parentContext;
        private readonly Dictionary<string, object> cache = new Dictionary<string, object>();
        private readonly Dictionary<string, ValuePair> ownValues = new Dictionary<string, ValuePair>();
        private readonly Dictionary<string, HashSet<string>> dependencyMap = new Dictionary<string, HashSet<string>>();
        private readonly WeakList<AnonymousValueBase> anonymousValues = new WeakList<AnonymousValueBase>();
        private readonly HashSet<Context> children = new HashSet<Context>();

        public Context() : this(null)
        {
        }

        public Context(Context parentContext)
        {
            this.parentContext = parentContext;
            parentContext?.children.Add(this);
        }

        public IEnumerable<Context> Children => children;

        public Context Parent => parentContext;

        public IReadOnlyDictionary<string, IValue> OwnValues => ownValues
            .Where(pair => pair.Value.Enumerable)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        //public Context Clone()
        //{
        //    var clone = new Context();
        //    foreach (var pair in ownValues)
        //    {
        //        clone.ownValues[pair.Key] = pair.Value;
        //    }

        //    foreach (var child in children)
        //    {
        //        var clonedChild = child.Clone();
        //        clonedChild.Attach(clone);
        //    }

        //    return clone;
        //}

        //public IReadOnlyDictionary<string, IValue> GetValues()
        //{
        //    var dict = new Dictionary<string, IValue>();
        //    AddOwnValues(dict);
        //    return dict;
        //}

        public void Attach(Context parent) => Attach(parent, true);

        public void Attach(Context parent, bool twoWay)
        {
            parentContext = parent;
            if (twoWay)
            {
                parent?.children.Add(this);
            }
        }

        public void Detach()
        {
            parentContext?.children.Remove(this);
        }

        public void Dispose()
        {
            Detach();
        }

        #region Reading

        public T Get<T>(string key)
        {
            key = key.Replace('/', '.');
            if (cache.TryGetValue(key, out var cachedValue))
            {
                if (cachedValue is T tCachedValue)
                {
                    return tCachedValue;
                }

                throw new InvalidOperationException($"Value '{key}' is not compatible with the requested type.");
            }

            var valueProvider = TryFindValue(key);
            if (valueProvider == null)
            {
                throw new KeyNotFoundException();
            }

            var tracker = new DependencyTracker(this, key);
            var result = valueProvider.Get(tracker);
            dependencyMap[key] = tracker.Dependencies;
            if (result is T t)
            {
                cache[key] = t;
                return t;
            }

            throw new InvalidOperationException($"Value '{key}' is not compatible with the requested type.");
        }

        public bool TryGet<T>(string key, out T result)
        {
            key = key.Replace('/', '.');
            if (cache.TryGetValue(key, out var cachedValue))
            {
                if (cachedValue is T tCachedValue)
                {
                    result = tCachedValue;
                    return true;
                }

                result = default;
                return false;
            }

            var valueProvider = TryFindValue(key);
            if (valueProvider == null)
            {
                result = default;
                return false;
            }

            var tracker = new DependencyTracker(this, key);
            var value = valueProvider.Get(tracker);
            dependencyMap[key] = tracker.Dependencies;
            if (value is T t)
            {
                cache[key] = value;
                result = t;
                return true;
            }

            result = default;
            return false;
        }

        public IAnonymousValue<T> Subscribe<T>(IValue<T> value)
        {
            var anonymousValue = new AnonymousValue<T>(this, value);
            anonymousValues.Add(anonymousValue);
            return anonymousValue;
        }

        public IAnonymousValue Subscribe(IValue value)
        {
            var anonymousValue = new AnonymousValue<object>(this, value);
            anonymousValues.Add(anonymousValue);
            return anonymousValue;
        }

        public IValue TryFindValue(string key)
        {
            return ownValues.TryGetValue(key, out var pair)
                ? pair.Value
                : parentContext?.TryFindValue(key);
        }

        public bool Has(string key)
        {
            return ownValues.ContainsKey(key) || parentContext != null && parentContext.Has(key);
        }

        public bool HasOwn(string key)
        {
            return ownValues.ContainsKey(key);
        }

        public bool HasEnumerable(string key)
        {
            return ownValues.TryGetValue(key, out var pair) && pair.Enumerable;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryGet(binder.Name, out result);
        }

        #endregion

        public void Set(string key, IValue value) => Set(key, value, true);

        public void Set(string key, IValue value, bool enumerable)
        {
            ownValues[key] = new ValuePair(value, enumerable);
            Refresh(key);
        }

        public bool DeleteOwn(string key)
        {
            if (ownValues.ContainsKey(key))
            {
                ownValues.Remove(key);
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

            var named = new HashSet<string>();
            var anonymous = new HashSet<AnonymousValueBase>();
            GetDependencies(key, named, anonymous);
            foreach (var item in named)
            {
                cache.Remove(item);
                dependencyMap.Remove(item);
            }

            foreach (var child in children)
            {
                if (!child.HasOwn(key))
                {
                    child.Refresh(key);
                }
            }

            foreach (var item in named)
            {
                RaiseChanged(item);
            }

            foreach (var item in anonymous)
            {
                item.RaiseValueChanged();
            }
        }

        private void GetDependencies(string key, HashSet<string> named, HashSet<AnonymousValueBase> anonymous)
        {
            if (named.Contains(key))
            {
                return;
            }

            named.Add(key);
            foreach (var pair in dependencyMap)
            {
                if (pair.Value.Contains(key))
                {
                    GetDependencies(pair.Key, named, anonymous);
                }
            }

            foreach (var value in anonymousValues)
            {
                if (value == null || anonymous.Contains(value))
                {
                    continue;
                }

                if (value.Dependencies.Contains(key))
                {
                    anonymous.Add(value);
                }
            }
        }

        private void RaiseChanged(string key)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key.Replace('.', '/')));
        }

        //private void AddOwnValues(Dictionary<string, IValue> dictionary)
        //{
        //    Parent?.AddOwnValues(dictionary);
        //    foreach (var pair in ownValues)
        //    {
        //        if (pair.Value.Enumerable)
        //        {
        //            dictionary[pair.Key] = pair.Value.Value;
        //        }
        //    }
        //}

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
