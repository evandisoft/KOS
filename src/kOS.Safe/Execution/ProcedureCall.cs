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
        public VariableStore Store { get; } // manages variable storing and retrieval

        /// <summary>
        /// Gets the current Opcode. 
        /// </summary>
        /// <value>The current opcode.</value>
        //Opcode currentOpcode = new OpcodeNOP();
        public bool IsFinished => instructionPointer>=Opcodes.Count;
 
        readonly IReadOnlyOpcodeList Opcodes;
        int instructionPointer = 0;
        public KOSThread Thread = null;


        public ProcedureCall(KOSThread thread,Procedure procedure)
        {
            Store = new VariableStore(thread.Process.ProcessManager.globalVariables);
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
            Thread = thread;
        }

        public Opcode CurrentOpcode => Opcodes[instructionPointer];

        /// <summary>
        /// Execute one instruction, and update the instructionPointers.
        /// </summary>
        public void ExecuteNextInstruction()
        {
            Deb.EnqueueExec("Current Opcode", CurrentOpcode);
            Deb.EnqueueExec("Stack for thread", "(" + Thread.ID, "is", Thread.Stack + ")");
            Deb.EnqueueExec("Store is", Store.scopeStack.Count);
            Deb.EnqueueOpcode(CurrentOpcode, "(ID:", Thread.ID + ")", "(Stack:", Thread.Stack.ToString() + ")");
            CurrentOpcode.Execute(Thread);
            Deb.EnqueueExec("In Execute. delta was", CurrentOpcode.DeltaInstructionPointer);
            instructionPointer +=CurrentOpcode.DeltaInstructionPointer;
        }
    }
}
