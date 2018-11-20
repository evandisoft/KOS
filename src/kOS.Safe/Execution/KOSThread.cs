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
        internal KOSProcess Process { get; }

        readonly coll.Stack<ProcedureExec> callStack = new coll.Stack<ProcedureExec>();

        public KOSThread(KOSProcess process){
            Process=process;
        }

        public ThreadStatus Execute(){
            Deb.logmisc ("Thread Execute. ProcedureCalls", callStack.Count);

            if (callStack.Count == 0) {
                return ThreadStatus.FINISHED;
            }

            var status = callStack.Peek().Execute();
            Deb.logmisc ("From ProcedureExec.Execute. status", status);

            switch(status){

            case ExecStatus.FINISHED:
                Deb.logmisc ("Removing ProcedureExec");
                callStack.Pop();
                if(callStack.Count==0){
                    return ThreadStatus.FINISHED;
                }
                return ThreadStatus.OK;

            default:
                return ThreadStatus.OK;

            }
        }

        public void AddProcedureExec(ProcedureExec exec)
        {
            callStack.Push(exec);
        }
    }
}
