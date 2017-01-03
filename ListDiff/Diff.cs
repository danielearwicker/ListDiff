using System.Collections.Generic;
using System.Linq;

namespace ListDiff
{
    public class Diff<T>
    {
        public Operation Operation { get; }
        
        public IReadOnlyList<T> Items { get; set; }
        
        public Diff(Operation operation, IReadOnlyList<T> items)
        {
            Operation = operation;
            Items = items;
        }

        public override string ToString()
        {
            var prettyText = string.Join("", Items.Select(t => t.ToString())).Replace('\n', '\u00b6');
            return "Diff(" + Operation + ",\"" + prettyText + "\")";
        }

        public override bool Equals(object obj)
        {
            // If parameter cannot be cast to Diff return false.
            var p = obj as Diff<T>;
            if (ReferenceEquals(p, null))
            {
                return false;
            }

            // Return true if the fields match.
            return Equals(p);
        }

        public bool Equals(Diff<T> obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // Return true if the fields match.
            return obj.Operation == Operation &&
                   obj.Items.SequenceEqual(Items);
        }

        public override int GetHashCode()
        {
            return Items.GetHashCode() ^ Operation.GetHashCode();
        }
    }
}