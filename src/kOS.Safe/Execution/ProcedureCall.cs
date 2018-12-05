using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using System.Collections.ObjectModel;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;
using System.Collections;
using kOS.Safe.DataStructures;

namespace kOS.Safe
{
    /// <summary>
    /// Procedure exec.
    /// This class holds a store and manages the instruction pointer.
    /// </summary>
    public class ProcedureCall {
        internal VariableStore Store { get; } // manages variable storing and retrieval

        /// <summary>
        /// Gets the current Opcode. 
        /// </summary>
        /// <value>The current opcode.</value>
        //Opcode currentOpcode = new OpcodeNOP();
        public bool IsFinished => instructionPointer>=Opcodes.Count;
 
        readonly IReadOnlyOpcodeList Opcodes;
        int instructionPointer = 0;
        KOSThread thread = null;

	    public ProcedureCall(KOSThread thread,Procedure procedure)
        {
            Store = new VariableStore(thread.Process.ProcessManager.globalVariables);
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
            this.thread = thread;
        }

        public Opcode CurrentOpcode => Opcodes[instructionPointer];

        public void Execute()
        {
            CurrentOpcode.Execute(thread);
            Deb.storeExec("In Execute. delta was", CurrentOpcode.DeltaInstructionPointer);
            instructionPointer+=CurrentOpcode.DeltaInstructionPointer;
        }
    }
}
