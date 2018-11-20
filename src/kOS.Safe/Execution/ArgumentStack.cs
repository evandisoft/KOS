using System;
using coll = System.Collections.Generic;

namespace kOS.Safe.Execution {
    public class ArgumentStack:coll.Stack<object> {
        readonly Store store;

        public ArgumentStack(Store store){
            this.store=store;
        }

        public object PopValue(bool barewordOkay = false)
        {
            var retval = this.Pop();
            Deb.logmisc("Getting value of", retval);
            var retval2 = store.GetValue(retval, barewordOkay);
            Deb.logmisc("Got value of", retval2);
            return retval2;
        }
    }
}
