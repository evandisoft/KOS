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
    public class ProcedureCall:IEnumerator<Opcode> {
        internal VariableStore Store { get; } // manages variable storing and retrieval

        /// <summary>
        /// Gets the current Opcode. 
        /// </summary>
        /// <value>The current opcode.</value>
        public Opcode Current => Opcodes[instructionPointer];

        object IEnumerator.Current => Current;


        readonly IReadOnlyOpcodeList Opcodes;
        int instructionPointer = 0;

	    public ProcedureCall(KOSThread thread,Procedure procedure)
        {
            Store = new VariableStore(thread.Process.ProcessManager.globalVariables);
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
        }

        /// <summary>
        /// Moves to the next opcode, updating the internal instruction counter
        /// based on the current opcode's DeltaInstructionPointer.
        /// The first opcode in a Procedure is a NOP that will be skipped over
        /// on the first call to MoveNext. If the NOP is attempted to be executed
        /// it will throw a NotImplementedException.
        /// </summary>
        public bool MoveNext()
        {
            Deb.logmisc("In Movenext. delta was", Opcodes[instructionPointer].DeltaInstructionPointer);

            instructionPointer+=Opcodes[instructionPointer].DeltaInstructionPointer;
            if (instructionPointer<Opcodes.Count) {
                return true;
            }
            if (instructionPointer>Opcodes.Count){
                throw new Exception(
                    "Opcodes Size is "+Opcodes.Count+
                    " InstructionPointerSize "+instructionPointer);
            }
            return false;
        }

        /// <summary>
        /// At the moment this just returns "NotImplementedException.
        /// I don't quite see a use for implementing this
        /// at the moment.
        /// </summary>
        public void Reset()
        {
            throw new NotImplementedException(
                "Reset of "+nameof(ProcedureCall)+" is not implemented.");
            //instructionPointer=0;
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
       public void Dispose()
        {
        }
    }
}
