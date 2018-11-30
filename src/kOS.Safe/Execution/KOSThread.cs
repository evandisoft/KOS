using System;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;
using System.Diagnostics;

namespace kOS.Safe {
    public enum ThreadStatus {
        OK,
        FINISHED,
        ERROR,
        GLOBAL_INSTRUCTION_LIMIT,
        THREAD_INSTRUCTION_LIMIT,
        TERMINATED,
        WAIT,
    }

    public class KOSThread {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;
        bool isTerminated = false;

        internal KOSProcess Process { get; }
        internal ArgumentStack Stack { get; }

        readonly coll.Stack<ProcedureExec> callStack = new coll.Stack<ProcedureExec>();
        // This counter keeps track of the threads own execution limit
        // TODO: Currently this is just set to the normal instructions
        // per update limit. I must give this a separate implementation
        // so that there can be a different limit per thread
        internal InstructionCounter ThreadInstructionCounter = new InstructionCounter();
        // This counter keeps track of the total limit on
        // instructions per update.
        internal InstructionCounter GlobalInstructionCounter;
        Stopwatch waitWatch = new Stopwatch();
        long timeToWaitInMilliseconds;

        public KOSThread(KOSProcess process)
        {
            Process=process;
            Stack=new ArgumentStack();
            GlobalInstructionCounter=Process.ProcessManager.GlobalInstructionCounter;
        }


        public ThreadStatus Execute()
        {
            Deb.logmisc("Executing thread", ID, "ProcedureExecs", callStack.Count);

            if (timeToWaitInMilliseconds>0 && 
                waitWatch.ElapsedMilliseconds>timeToWaitInMilliseconds){
                // Use one instruction up so that if all threads are waiting
                // we can still eventually return to allow the fixedupdate
                waitWatch.Reset();
                if(GlobalInstructionCounter.Continue()){
                    return ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
                } 
                return ThreadStatus.WAIT;
            }

            if (callStack.Count == 0) { return ThreadStatus.FINISHED; }
            if (isTerminated) { return ThreadStatus.TERMINATED; }

            // run the procedure.
            var status = ThreadStatus.OK;
            while (status==ThreadStatus.OK) {
                status=ExecuteProcedure(callStack.Peek());
            }
            Deb.logmisc("Exiting thread", ID, "with status", status);

            return status;
        }

        ThreadStatus ExecuteProcedure(ProcedureExec procedureExec)
        {
            if (!GlobalInstructionCounter.Continue()) {
                GlobalInstructionCounter.Reset();
                return ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
            }
            if (!ThreadInstructionCounter.Continue()) {
                ThreadInstructionCounter.Reset();
                return ThreadStatus.THREAD_INSTRUCTION_LIMIT;
            }

            var status = procedureExec.Execute();

            switch (status) {

            case ExecStatus.ERROR:
                return ThreadStatus.ERROR;
            case ExecStatus.WAIT:
                return ThreadStatus.WAIT;
            case ExecStatus.RETURN:
            case ExecStatus.FINISHED:
                Deb.logmisc("Removing ProcedureExec");
                callStack.Pop();
                if (callStack.Count==0) {
                    return ThreadStatus.FINISHED;
                }
                //callStack.Peek().Stack.Push(this.retval);
                return ThreadStatus.OK;
            default:
                return ThreadStatus.OK;
            }
        }

        // Create a new ProcedureExec, and add it to the stack, 
        // to be executed next time this thread runs.
        // Called by OpcodeCall.Execute
        // Used only for calls made within this thread.
        // Because we the stack is local to this thread.
        public void Call(Procedure procedure)
        {
            ProcedureExec exec = new ProcedureExec(this, procedure);
            callStack.Push(exec);
        }

        // Manually add in arguments when this is called via 'run'
        // because we are not sharing a global stack.
        public void CallWithArgs(Procedure procedure, coll.List<object> args)
        {
            ProcedureExec exec = new ProcedureExec(this, procedure);
            callStack.Push(exec);
            Stack.Push(new KOSArgMarkerType());
            for (int i = args.Count-1;i>=0;i--) {
                Stack.Push(args[i]);
            }
        }

        //object retval;
        //// OpcodeReturn.Execute calls this to set the return value
        //public void SetReturnValue(object retval){
        //    this.retval=retval;
        //}

        public void Terminate()
        {
            isTerminated=true;
        }

        internal void Wait(double arg)
        {
            timeToWaitInMilliseconds=Convert.ToInt64(arg)*1000;
            waitWatch.Start();
        }
    }
}
