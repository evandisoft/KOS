using System;
namespace kOS.Safe.Execution {
    public class KOSTrigger:KOSThread {
        public KOSTrigger(KOSProcess process) : base(process) {
        }
    }
}
