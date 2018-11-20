using System;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;

namespace kOS.Safe
{
    public enum ThreadStatus
    {
        OK,
        FINISHED
    }

    public class KOSThread
    {
        internal KOSProcess process;

        coll.Stack<ProcedureCall> callStack = new coll.Stack<ProcedureCall>();

        public KOSThread(KOSProcess process){
            this.process=process;
        }


        public ThreadStatus Execute(){
            Deb.logmisc ("Thread Execute. ProcedureCalls", callStack.Count);

            if (callStack.Count == 0) {
                return ThreadStatus.FINISHED;
            }

            var status = callStack.Peek().Execute();
            Deb.logmisc ("From ProcedureCall Execute. status", status);

            switch(status){

            case ProcedureCallStatus.FINISHED:
                Deb.logmisc ("Removing ProcedureCall");
                callStack.Pop();
                return ThreadStatus.OK;

            default:
                return ThreadStatus.OK;

            }
        }


        public void AddProcedureCall(ProcedureCall procedureCall)
        {
            callStack.Push(procedureCall);
        }
    }
}
