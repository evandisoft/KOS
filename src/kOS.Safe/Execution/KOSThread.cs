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
        FINISHED
    }

    public class KOSThread
    {
        internal KOSProcess Process { get; }

        readonly coll.Stack<ProcedureExec> callStack = new coll.Stack<ProcedureExec>();


        public KOSThread(KOSProcess process){
            Process=process;
        }

        public void Call(Procedure procedure){
            ProcedureExec exec = new ProcedureExec(this,procedure);
            callStack.Push(exec);
        }

        object retval;
        public void Return(object retval){
            this.retval=retval;
        }

        public ThreadStatus Execute(){
            Deb.logmisc("Thread Execute. ProcedureExecs", callStack.Count);

            if (callStack.Count == 0) {
                return ThreadStatus.FINISHED;
            }

            var status = callStack.Peek().Execute();
            Deb.logmisc("From ProcedureExec.Execute. status", status);

            switch(status){

            case ExecStatus.FINISHED:
                Deb.logmisc("Removing ProcedureExec");
                callStack.Pop();
                if(callStack.Count==0){
                    return ThreadStatus.FINISHED;
                } 
                callStack.Peek().Stack.Push(retval);
                return ThreadStatus.OK;

            default:
                return ThreadStatus.OK;

            }
        }


    }
}
