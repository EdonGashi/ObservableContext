using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public class LiteralValue<T> : ValueBase<T>
    {
        public LiteralValue(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public override T Get(IContext context) => Value;

        public override JToken Tokenize() => Value is ITokenizable tokenizable
            ? tokenizable.Tokenize()
            : new JValue(Value);
    }
}
