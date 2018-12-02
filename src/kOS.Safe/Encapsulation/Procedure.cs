using System;
using kOS.Safe.Compilation;
using System.Collections.Generic;
using kOS.Safe.Execution;
using kOS.Safe.Utilities;
using System.Collections.ObjectModel;
using kOS.Safe.DataStructures;
// evandisoft TODO: Though I have made this inherit from Structure, I have not
// overriden any of the default behavior and I imagine some of it should be
// changed.
namespace kOS.Safe.Encapsulation
{
    /// <summary>
    /// Procedure is currently only instantiated in two places. One is 
    /// when an OpcodePushDelegate is being executed, and the other is
    /// the encapsulation of the main program code in ProgramBuilder2.
    /// OpcodePushDelegate does not store a Procedure, but rather a list
    /// of opcodes. This Procedure shares its list of opcodes with every
    /// other Procedure instantiated by the same OpcodePushDelegate.
    /// </summary>
    [KOSNomenclature("Procedure")]
    public class Procedure:Structure
    {
        /// <summary>
        /// Opcodes may at runtime end up being shared by 
        /// multiple separate instances of the Procedure class with different 
        /// closures (not just different instances of the ProcedureCall class), 
        /// and any changes made here to the underlying list could lead to 
        /// bizarre and random runtime behavior.
        /// 
        /// The IReadOnlyOpcodes interface is being used to strongly hint
        /// that you should not modify the underlying list at this point.
        /// </summary>
        internal IReadOnlyOpcodeList Opcodes { get;}
        internal List<Mapping> Closure { get; } = new List<Mapping>();

        public Procedure(IReadOnlyOpcodeList opcodes,VariableStore closure=null)
        {
            Opcodes = opcodes;
            if(closure!=null){
                foreach(var level in closure.scopeStack){
                    Closure.Add(level);
                }
            }
            Deb.logmisc("closure in Procedure constructor is", closure);
        }

        public ProcedureCall Call(KOSThread thread){
            return new ProcedureCall(thread,this);
        }
    }
}
