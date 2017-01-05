# ListDiff
C# algorithm for diffing two lists of objects. The implementation and tests are heavily based on
[Google's Diff-Match-Patch](https://github.com/lqc/google-diff-match-patch) - that's a 3rd party mirror of the
code that was [originally hosted here](https://code.google.com/p/google-diff-match-patch/).

I've removed all the patching/merging stuff and anything specific to text, so what remains is generalised to
support comparing two `IReadOnlyList<T>` where T can be any type comparable with `EqualityComparer<T>.Default`,
e.g. one overriding `Equals`.

My purpose for this is to be able to compare two lists of objects, `A` and `B`, and get a short description of 
how to modify `A` to make it the same as `B`.

There is a single public method to call in the `ListDiff` namespace, `ListDiff.Compare`:

```csharp
using static ListDiff.ListDiff;

var diff = Compare(list1, list2);
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

That is the complete API. So for example:

```csharp
using System;
using static ListDiff.ListDiff;

namespace DiffThing
{
    class Program
    {
        static void Main(string[] args)
        {
            var diffs = Compare(
                "The quick brown fox jumped over the dog".ToCharArray(),
                "The quack brown animal jumped the lazy dog".ToCharArray());

            foreach (var diff in diffs)
            {
                Console.WriteLine(diff);
            }
        }
    }
}
```

This prints:

```
Diff(Equal,"The qu")
Diff(Delete,"i")
Diff(Insert,"a")
Diff(Equal,"ck brown ")
Diff(Delete,"fox")
Diff(Insert,"animal")
Diff(Equal," jumped ")
Diff(Delete,"over ")
Diff(Equal,"the")
Diff(Insert," lazy")
Diff(Equal," dog")
```
