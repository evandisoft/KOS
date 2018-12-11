using System;
using kOS.Safe.Encapsulation;

namespace kOS.Safe.Execution {
    public class CallBack:KOSTrigger {
        public Structure returnVal;

        public CallBack(KOSProcess process) : base(process) {
        }
    }
}
