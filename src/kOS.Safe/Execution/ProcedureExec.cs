using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;
using System.Collections;

namespace kOS.Safe
{
    public enum ExecStatus {
        OK,
        FINISHED,
        GLOBAL_INSTRUCTION_LIMIT,
        THREAD_INSTRUCTION_LIMIT,
        RETURN,
        CALL,
        ERROR,
        WAIT,
    }

    // Almost every single call made by an opcode does not have to be made
    // at the top-level. This class passes itself to rewritten opcode
    // "Execute" functions that now take a ProcedureExec, whereas
    // before they took an "ICpu". The opcode's "Execute" can now access
    // different parts of the program for different calls.
    // This class contains references to a 'Store' that does the work of 
    // storing and retrieving things (other than pushing and popping
    // stack arguments, which is something ArgumentStack does).
    // It also contains a reference to its thread, which in turn contains
    // a reference to it's process. (Some opcodes like "addTrigger" will
    // need to make calls at the level of the process.)
    // Store holds a reference to the global scope
    public class ProcedureExec:IEnumerator<Opcode> {
        internal VariableStore Store { get; } // manages variable storing and retrieval

        public Opcode Current => Opcodes[instructionPointer];

        object IEnumerator.Current => Current;

        readonly List<Opcode> Opcodes;
        int instructionPointer = 0;

	    public ProcedureExec(KOSThread thread,Procedure procedure)
        {
            Store = new VariableStore(thread.Process.ProcessManager.globalVariables);
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
        }

        internal object PopValue(bool barewordOkay = false)
        {
            var retval = Stack.Pop();
            Deb.logmisc("Getting value of", retval);
            var retval2 = Store.GetValue(retval, barewordOkay);
            Deb.logmisc("Got value of", retval2);
            return retval2;
        }

        public bool MoveNext()
        {
            instructionPointer+=Opcodes[instructionPointer].DeltaInstructionPointer;
            if(instructionPointer<Opcodes.Count){
                return true;
            } 
            if(instructionPointer>Opcodes.Count){
                throw new Exception(
                    "Opcodes Size is "+Opcodes.Count+
                    " InstructionPointerSize "+instructionPointer);
            }
            return false;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
