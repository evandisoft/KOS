using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;

namespace kOS.Safe
{

    public enum ProcedureCallStatus
    {
        OK,
        FINISHED
    }

    // Almost every single call made by an opcode does not have to be made
    // at the top-level. This class passes itself to rewritten opcode
    // "Execute" functions that now take a ProcedureCall, whereas
    // before they took an "ICpu". The opcode's "Execute" can now access
    // different parts of the program for different calls.
    // This class contains references to a 'Store' that does the work of 
    // storing and retrieving things (other than pushing and popping
    // stack arguments, which is something this class does).
    // It also contains a reference to its thread, which in turn contains
    // a reference to it's process. (Some opcodes like "addTrigger" will
    // need to make calls at the level of the process.)
    // Store holds a reference to the global scope
    public class ProcedureCall
    {
        internal readonly KOSThread thread;

        List<Opcode> Opcodes;
        int instructionPointer = 0;
        internal Store store; // stores all the variables in proper scope
        coll.Stack<object> argumentStack = new coll.Stack<object>();

	    public ProcedureCall(KOSThread thread,List<Opcode> Opcodes)
        {
            this.thread = thread;
            store = new Store(thread.process.processManager.globalVariables);
            this.Opcodes = Opcodes;
        }

        public ProcedureCallStatus Execute()
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

            Deb.logmisc ("Current Opcode", opcode.Label,opcode);
            opcode.Execute(this);
            int delta = opcode.DeltaInstructionPointer;
            instructionPointer += delta;
            Deb.logmisc ("delta was", delta, 
                         ". New instruction pointer", instructionPointer);

            if (instructionPointer==Opcodes.Count){
                return ProcedureCallStatus.FINISHED;
            }
            if(instructionPointer<Opcodes.Count){
		        return ProcedureCallStatus.OK;
            }

            throw new Exception ("Instruction way out of bounds!");
        }

        public void PushArgument(object item)
        {
            Deb.logmisc("pushing item", item, "to the stack");
            argumentStack.Push(item);
        }

        public object PopArgument()
        {
            var retval= argumentStack.Pop();
            Deb.logmisc("popping", retval, "from the stack");
            return retval;
        }

        public object PopValueArgument(bool barewordOkay = false)
        {
            var retval = PopArgument();
            Deb.logmisc("Getting value of", retval);
            var retval2=store.GetValue(retval, barewordOkay);
            Deb.logmisc("Got value of", retval2);
            return retval2;
        }
    }
}
