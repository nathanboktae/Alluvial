using System;
using System.Diagnostics;

namespace Alluvial
{
    [DebuggerStepThrough]
    internal class ChronologicalCursor : IIncrementableCursor
    {
        private DateTimeOffset position;

        public ChronologicalCursor(
            DateTimeOffset position = default(DateTimeOffset),
            bool ascending = true)
        {
            Ascending = ascending;
            this.position = position;
        }

        public bool Ascending { get; private set; }

        public dynamic Position
        {
            get
            {
                return position;
            }
        }

        public void AdvanceBy(dynamic amount)
        {
            if (Ascending)
            {
                position = position.Add((TimeSpan) amount);
            }
            else
            {
                position = position.Subtract((TimeSpan) amount);
            }
        }

        public void AdvanceTo(dynamic position)
        {
            this.position = position;
        }
    }
}