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
        coll.Stack<ProcedureCall> callStack = new coll.Stack<ProcedureCall> ();

        public void AddProcedureCall(ProcedureCall procedureCall)
        {
            callStack.Push(procedureCall);
        }

        public ThreadStatus Execute(){
            if (callStack.Count == 0) {
                return ThreadStatus.FINISHED;
            }

            var status = callStack.Peek().Execute(this);
            switch(status){

            case ProcedureCallStatus.FINISHED:
                callStack.Pop();
                return ThreadStatus.OK;

            default:
                return ThreadStatus.OK;

            }
        }
    }
}
