using System;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;

namespace kOS.Safe
{
    public enum ThreadStatus
    {
        OK,
        FINISHED,
        ERROR,
        GLOBAL_INSTRUCTION_LIMIT,
        THREAD_INSTRUCTION_LIMIT,
        TERMINATED,
    }



    public class KOSThread
    {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;
        bool isTerminated = false;

        internal KOSProcess Process { get; }

        readonly coll.Stack<ProcedureExec> callStack = new coll.Stack<ProcedureExec>();
        internal InstructionCounter ThreadInstructionCounter = new InstructionCounter();
        // This counter keeps track of the total limit on
        // instructions per update.
        internal InstructionCounter GlobalInstructionCounter;
        public KOSThread(KOSProcess process){
            Process=process;
            GlobalInstructionCounter=Process.ProcessManager.GlobalInstructionCounter;

        }


        public ThreadStatus Execute()
        {
            Deb.logmisc("Executing thread",ID,"ProcedureExecs", callStack.Count);

            if (callStack.Count == 0) { return ThreadStatus.FINISHED; }
            if (isTerminated) { return ThreadStatus.TERMINATED; }

            // run the procedure.
            var status = ThreadStatus.OK;
            while(status==ThreadStatus.OK){
                status=ExecuteProcedure(callStack.Peek());
            }
            Deb.logmisc("Exiting thread",ID,"with status", status);

            return status;
        }

        ThreadStatus ExecuteProcedure(ProcedureExec procedureExec){
            if (!GlobalInstructionCounter.Continue()){
                GlobalInstructionCounter.Reset();
                return ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
            }
            if (!ThreadInstructionCounter.Continue()){
                ThreadInstructionCounter.Reset();
                return ThreadStatus.THREAD_INSTRUCTION_LIMIT;
            }

            var status=procedureExec.Execute();

            switch (status) {

            case ExecStatus.ERROR:
                return ThreadStatus.ERROR;
            case ExecStatus.RETURN:
            case ExecStatus.FINISHED:
                Deb.logmisc("Removing ProcedureExec");
                callStack.Pop();
                if (callStack.Count==0) {
                    return ThreadStatus.FINISHED;
                }
                callStack.Peek().Stack.Push(this.retval);
                return ThreadStatus.OK;
            default:
                return ThreadStatus.OK;
            }
        }

        // creates a new ProcedureExec, and adds it to the stack, 
        // to be executed next time this thread runs.
        // Called by OpcodeCall.Execute
        public void Call(Procedure procedure){
            ProcedureExec exec = new ProcedureExec(this,procedure);
            callStack.Push(exec);
        }

        object retval;
        // OpcodeReturn.Execute calls this to set the return value
        public void SetReturnValue(object retval){
            this.retval=retval;
        }

        public void Terminate(){
            isTerminated=true;
        }
    }
}
