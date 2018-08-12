# ObservableContext

MobX-like change tracker for .NET Standard.

Values are immutable, serializable, and memoized. They are stored in keys inside Context objects. Contexts can form trees, with the ability to inherit values from parent contexts.

## Example

Simplified example of how this library is being used:

```json
{
  "type": "document",
  "text.color": "red",
  "pages": [
    {
      "type": "page",
      "content": "Page 1 content"
    },
    {
      "type": "page",
      "text.color": "green",
      "content": "Page 2 content"
    },
    {
      "type": "page",
      "content": "Page 3 content"
    }
  ]
}
```

Page 1 and 3 will inherit `text.color` of red, while page 2 will display a green text. Whenever `text.color` changes, values that depend on that property will be re-evaluated.
