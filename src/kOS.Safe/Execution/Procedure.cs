using System;
using kOS.Safe.Compilation;
using kOS.Safe.Execution;
using System.Collections.Generic;
using coll = System.Collections.Generic;

namespace kOS.Safe
{
    public class Procedure
    {
        private Opcodes Opcodes { get; set; }
        // TODO: need to also store a closure here that is then passed
        // into the ProcedureCall 

        public Procedure(Opcodes Opcodes)
        {
            this.Opcodes = Opcodes;
        }

        //public ProcedureCall Call(){
        //    return new ProcedureCall(Opcodes);
        //}
    }
}
