using System;
using kOS.Safe.Compilation;
using System.Collections.Generic;
using kOS.Safe.Execution;
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
        internal List<Mapping> Closure { get; } = new List<Mapping>();

        public Procedure(List<Opcode> Opcodes,VariableStore closure=null)
        {
            // Sentinal Opcode that makes implementation of ProcedureExec as an 
            // IEnumerator<Opcode> simpler.
            // IEnumerator requires "MoveNext" to be done prior to any access to
            // Current. And MoveNext accesses the current opcode's 
            // "DeltaInstructionPointer". 
            Opcodes.Insert(0, new OpcodeNOP());
            this.Opcodes = Opcodes;
            if(closure!=null){
                foreach(var level in closure.scopeStack){
                    Closure.Add(level);
                }
            }
            Deb.logmisc("closure in Procedure constructor is", closure);
        }
    }
}
