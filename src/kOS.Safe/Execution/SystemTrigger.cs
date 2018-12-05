using kOS.Safe.Execution;

namespace kOS.Safe {
    public class SystemTrigger : KOSTrigger {
        public SystemTrigger(string name,KOSProcess process) : base(process)
        {
            Name=name;
        }

        public string Name { get; }

    }
}