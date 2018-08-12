using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public abstract class CoercedValue : IValue
    {
        protected CoercedValue(IValue innerValue)
        {
            InnerValue = innerValue ?? throw new ArgumentException(nameof(innerValue));
        }

        public IValue InnerValue { get; }

        public JToken Tokenize() => InnerValue.Tokenize();

        public Type Type => InnerValue.Type;

        object IValue.Get(IContext context) => InnerValue.Get(context);
    }

    public class CoercedValue<T> : CoercedValue, IValue<T>
    {
        public CoercedValue(IValue innerValue)
            : base(innerValue)
        {
        }

        public T Get(IContext context)
        {
            var result = InnerValue.Get(context);
            if (result is T t)
            {
                return t;
            }

            if (result is IConvertible c)
            {
                return (T)c.ToType(typeof(T), CultureInfo.InvariantCulture);
            }

            throw new InvalidOperationException($"Invalid type returned from a value. Expected '{typeof(T)}', got '{result?.GetType().ToString() ?? "null"}'.");
        }
    }

    public class CoercedCollection<T> : CoercedValue, IValue<IEnumerable<T>>
    {
        public CoercedCollection(IValue innerValue)
            : base(innerValue)
        {
        }

        public IEnumerable<T> Get(IContext context)
        {
            var result = InnerValue.Get(context);
            if (result is T t)
            {
                return new[] { t };
            }

            if (result is IEnumerable<T> enumerable)
            {
                return enumerable;
            }

            if (result is IEnumerable<object> nonGeneric)
            {
                return nonGeneric.OfType<T>().ToList();
            }

            throw new InvalidOperationException($"Invalid type returned from a value. Expected 'IEnumerable<{typeof(T)}>'.");
        }
    }
}