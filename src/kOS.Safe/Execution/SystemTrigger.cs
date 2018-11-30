namespace kOS.Safe {
    public class SystemTrigger : KOSThread {
        private string _name;

        public SystemTrigger(string name,KOSProcess process) : base(process)
        {
            _name=name;
        }

        public string Name { get => _name; internal set => _name=value; }
    }
}