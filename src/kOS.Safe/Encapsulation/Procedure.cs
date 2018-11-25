using System;
using kOS.Safe.Compilation;
using System.Collections.Generic;
using kOS.Safe.Utilities;
// evandisoft TODO: Though I have made this inherit from Structure, I have not
// overriden any of the default behavior and I imagine some of it should be
// changed.
namespace kOS.Safe.Encapsulation
{
    [KOSNomenclature("Procedure")]
    public class Procedure:Structure
    {
        internal List<Opcode> Opcodes { get;}
        // TODO: need to also store a closure here that is then passed
        // into the ProcedureCall 

        public Procedure(List<Opcode> Opcodes)
        {
            this.Opcodes = Opcodes;
        }
    }
}
