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
        Opcode currentOpcode = new OpcodeNOP();
        internal KOSThread thread;
        public bool IsFinished => instructionPointer>=Opcodes.Count;
 
        readonly IReadOnlyOpcodeList Opcodes;
        int instructionPointer = 0;

	    public ProcedureCall(KOSThread thread,Procedure procedure)
        {
            this.thread=thread;
            Store = new VariableStore(thread.Process.ProcessManager.globalVariables);
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
        }

        /// <summary>
        /// Executes the current opcode, then updates the instructionPointer.
        /// </summary>
        public Opcode Execute()
        {
            currentOpcode=Opcodes[instructionPointer];
            Deb.storeOpcode(currentOpcode);
            Deb.logmisc("Current Opcode", currentOpcode.Label, currentOpcode);
            currentOpcode.Execute(thread);
            Deb.logmisc("In Execute. delta was", currentOpcode.DeltaInstructionPointer);
            instructionPointer+=currentOpcode.DeltaInstructionPointer;

            return currentOpcode;
        }
    }
}
