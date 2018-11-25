using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;

namespace kOS.Safe
{
    public enum ExecStatus {
        OK,
        FINISHED
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
    public class ProcedureExec {
        internal KOSThread Thread { get; }

        internal ArgumentStack Stack { get; } // manages the argument stack
        internal VariableStore Store { get; } // manages variable storing and retrieval
        internal SafeSharedObjects Shared { get; }

        readonly List<Opcode> Opcodes;
        int instructionPointer = 0;


	    public ProcedureExec(KOSThread thread,Procedure procedure)
        {
            Thread = thread;
            Store = new VariableStore(Thread.Process.ProcessManager.globalVariables);
            Stack=new ArgumentStack();
            Shared=Thread.Process.ProcessManager.shared;
            Store.AddClosure(procedure.closure);
            this.Opcodes = procedure.Opcodes;
            Deb.logmisc("after ProcedureExec closure added, store is", Store);

        }

        public ExecStatus Execute()
        {
            Deb.logmisc("ProcedureExec Execute. instructionPointer", instructionPointer,
                        "total opcodes",Opcodes.Count);

            Opcode opcode = Opcodes[instructionPointer];

            Deb.storeOpcode(opcode);

            Deb.logmisc("Current Opcode", opcode.Label,opcode);
            try{
                opcode.Execute(this);
                Deb.logmisc("Current store is", Store);
            }catch(Exception e){
                Deb.logmisc(e);
                return ExecStatus.FINISHED;
            }

            int delta = opcode.DeltaInstructionPointer;
            instructionPointer += delta;
            Deb.logmisc("delta was", delta, 
                         ". New instruction pointer", instructionPointer);

            if (instructionPointer==Opcodes.Count || opcode.GetType()==typeof(OpcodeReturn)){
                Deb.logmisc("Reached the end of the procedure.");
                return ExecStatus.FINISHED;
            }
            if(instructionPointer<Opcodes.Count){
		        return ExecStatus.OK;
            }

            throw new Exception("Instruction way out of bounds!");
        }

        internal object PopValue(bool barewordOkay = false)
        {
            var retval = Stack.Pop();
            Deb.logmisc("Getting value of", retval);
            var retval2 = Store.GetValue(retval, barewordOkay);
            Deb.logmisc("Got value of", retval2);
            return retval2;
        }
    }
}
