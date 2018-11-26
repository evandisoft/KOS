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


        public KOSThread(KOSProcess process){
            Process=process;
        }

        public ThreadStatus Execute()
        {
            Deb.logmisc("Thread Execute. ProcedureExecs", callStack.Count);

            if (callStack.Count == 0) { return ThreadStatus.FINISHED; }
            if (isTerminated) { return ThreadStatus.TERMINATED; }

            // run the procedure.
            var status = callStack.Peek().Execute();
            Deb.logmisc("From ProcedureExec.Execute. status", status);

            return HandleExecStatus(status);
        }

        ThreadStatus HandleExecStatus(ExecStatus status){
            switch (status) {

            case ExecStatus.THREAD_INSTRUCTION_LIMIT:
                return ThreadStatus.THREAD_INSTRUCTION_LIMIT;
            case ExecStatus.GLOBAL_INSTRUCTION_LIMIT:
                return ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
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
