using kOS.Safe.Execution;

namespace kOS.Safe {
    public class SystemTrigger : KOSThread {
        public SystemTrigger(string name,KOSProcess process) : base(process)
        {
            Name=name;
            // This makes it so that the argbottom opcode is happy. This
            // trigger will never be called with an argument
            // evandisoft TODO: May remove this after i've found
            // a way to modify the actual code of the procedure
            // we're creating.
            //Stack.Push(new KOSArgMarkerType());
        }

        public string Name { get; }
    }
}