using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public class ValueList : ValueBase<IEnumerable<object>>
    {
        public ValueList(IEnumerable<IValue> values)
        {
            Values = values ?? new IValue[0];
        }

        public IEnumerable<IValue> Values { get; }

        public override IEnumerable<object> Get(IContext context) => Values?.Select(v => v.Get(context));

        public override JToken Tokenize()
        {
            return new JArray(Values.Select(value => value.Tokenize()).ToList());
        }
    }
}