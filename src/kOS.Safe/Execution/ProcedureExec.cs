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
        FINISHED,
        GLOBAL_INSTRUCTION_LIMIT,
        THREAD_INSTRUCTION_LIMIT,
        RETURN,
        CALL,
        ERROR,
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
        internal InstructionCounter GlobalInstructionCounter;
        internal InstructionCounter ThreadInstructionCounter;

	    public ProcedureExec(KOSThread thread,Procedure procedure)
        {
            Thread = thread;
            Store = new VariableStore(Thread.Process.ProcessManager.globalVariables);
            Stack=new ArgumentStack();
            Shared=Thread.Process.ProcessManager.shared;
            Store.AddClosure(procedure.Closure);
            Opcodes = procedure.Opcodes;
            // This counter keeps track of the total limit on
            // instructions per update.
            GlobalInstructionCounter=Thread.Process.ProcessManager.GlobalInstructionCounter;
            // This counter ensures that a thread eventually is stopped
            // after the thread instruction limit is reached.
            // Currently this counter uses the exact same value as
            // the global counter.
            // (SafeHouse.Config.InstructionsPerUpdate)
            ThreadInstructionCounter=Thread.ThreadInstructionCounter;
        }

        public ExecStatus Execute()
        {
            while(GlobalInstructionCounter.Continue()) {
                if(!ThreadInstructionCounter.Continue()){
                    ThreadInstructionCounter.Reset();
                    return ExecStatus.THREAD_INSTRUCTION_LIMIT;
                }
                Deb.logmisc("ProcedureExec Execute. instructionPointer", instructionPointer,
                        "total opcodes", Opcodes.Count);

                Opcode opcode = Opcodes[instructionPointer];

                Deb.storeOpcode(opcode);

                Deb.logmisc("Current Opcode", opcode.Label, opcode);
                try {
                    opcode.Execute(this);
                } catch (Exception e) {
                    Deb.logmisc(e);
                    return ExecStatus.ERROR;
                }

                instructionPointer += opcode.DeltaInstructionPointer;

                switch (opcode.Code) {

                case (ByteCode.RETURN):
                    return ExecStatus.RETURN;
                case (ByteCode.CALL):
                    return ExecStatus.CALL;
                }

                if (instructionPointer==Opcodes.Count || opcode.GetType()==typeof(OpcodeReturn)) {
                    Deb.logmisc("Reached the end of the procedure.");
                    Thread.SetReturnValue(0);
                    return ExecStatus.FINISHED;
                } 
                if (instructionPointer>Opcodes.Count){
                    throw new Exception("Instruction way out of bounds!");
                }
            }
            GlobalInstructionCounter.Reset();
            return ExecStatus.GLOBAL_INSTRUCTION_LIMIT;
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
