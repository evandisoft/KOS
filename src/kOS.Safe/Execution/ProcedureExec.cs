using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;

namespace kOS.Safe
{

    public enum ExecStatus
    {
        OK,
        FINISHED
    }

    // Almost every single call made by an opcode does not have to be made
    // at the top-level. This class passes itself to rewritten opcode
    // "Execute" functions that now take a Call, whereas
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

        internal Store Store { get; } // manages variable storing and retrieval
        internal ArgumentStack ArgumentStack { get; } // manages the argument stack

        readonly List<Opcode> Opcodes;
        int instructionPointer = 0;

	    public ProcedureExec(KOSThread thread,List<Opcode> Opcodes)
        {
            Thread = thread;
            Store = new Store(thread.Process.ProcessManager.globalVariables);
            ArgumentStack=new ArgumentStack(Store);
            this.Opcodes = Opcodes;
        }

        public ExecStatus Execute()
        {
            Deb.logmisc("ProcedureCall Execute. instructionPointer", instructionPointer,
                        "total opcodes",Opcodes.Count);

            Opcode opcode = Opcodes[instructionPointer];

            if (opcode.Code != ByteCode.EOF) { // Log the opcodes to the opcode queue
                if (CPU.OpcodeLogQueue.Count > CPU.OpcodeQueueLen) {
                    CPU.OpcodeLogQueue.Dequeue();
                }
                CPU.OpcodeLogQueue.Enqueue(opcode);
            }

            Deb.logmisc("Current Opcode", opcode.Label,opcode);
            try{
                opcode.Execute(this);
            }catch(Exception e){
                Deb.logmisc(e);
                return ExecStatus.FINISHED;
            }
            int delta = opcode.DeltaInstructionPointer;
            instructionPointer += delta;
            Deb.logmisc("delta was", delta, 
                         ". New instruction pointer", instructionPointer);

            if (instructionPointer==Opcodes.Count){
                return ExecStatus.FINISHED;
            }
            if(instructionPointer<Opcodes.Count){
		        return ExecStatus.OK;
            }

            throw new Exception("Instruction way out of bounds!");
        }


    }
}
