using System;

namespace kOS.Safe.Execution
{


    /// <summary>
    /// ArgMarkerType literally serves no purpose whatsoever other
    /// than to just be a mark of where on the stack is the bottom of
    /// the aruments to something.  If an object is of this type, then
    /// that means it's the argument botttom marker.
    /// </summary>
    public class KOSArgMarkerType {
        static long NextID = 0;
        public long ID { get; } = NextID++;

        public override string ToString()
        {
            return "_KOSArgMarker_"+ID;
        }
    }
}
