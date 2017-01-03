# ListDiff
C# algorithm for diffing two lists of objects. The implementation and tests are heavily based on:

    https://github.com/lqc/google-diff-match-patch

originally Google project, based at:

    https://code.google.com/p/google-diff-match-patch/

I've removed all the patching/merging stuff and anything specific to text, so what remains is generalised to
support comparing two `IReadOnlyList<T>` where T can be any type comparable with `EqualityComparer<T>.Default`,
e.g. one overriding `Equals`.

My purpose for this is to be able to compare two lists of objects, `A` and `B`, and get a short description of 
how to modify `A` to make it the same as `B`.

There is a single public method to call, `ListDiff.Compare`:

```csharp
var diff = ListDiff.Compare(list1, list2);
```

The returned sequence is of `Diff` objects, like this:

```csharp
public class Diff<T>
{
    public Operation Operation { get; }       
    public IReadOnlyList<T> Items { get; set; }
```

Finally, Operations are:

```csharp
public enum Operation
{
    Delete, Insert, Equal
}
```

That is the complete API.