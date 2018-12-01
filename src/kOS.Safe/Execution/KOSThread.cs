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

    public class KOSThread:IExec {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;
        bool isTerminated = false;

        internal KOSProcess Process { get; }
        internal ArgumentStack Stack { get; }

        ProcedureExec CurrentProcedure => callStack.Peek();
        VariableStore CurrentStore => CurrentProcedure.Store;

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

            if(isWaiting(out ThreadStatus threadStatus)){
                return threadStatus;
            }

            if (callStack.Count == 0) { return ThreadStatus.FINISHED; }
            if (isTerminated) { return ThreadStatus.TERMINATED; }

            // run the procedure.
            var status = ThreadStatus.OK;
            while (status==ThreadStatus.OK) {
                status=ExecuteCurrentProcedure();
            }
            Deb.logmisc("Exiting thread", ID, "with status", status);

            return status;
        }



        ThreadStatus ExecuteCurrentProcedure()
        {
            if (!GlobalInstructionCounter.Continue()) {
                GlobalInstructionCounter.Reset();
                return ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
            }
            if (!ThreadInstructionCounter.Continue()) {
                ThreadInstructionCounter.Reset();
                return ThreadStatus.THREAD_INSTRUCTION_LIMIT;
            }
            if (!CurrentProcedure.MoveNext()){
                Deb.logmisc("Removing ProcedureExec");
                callStack.Pop();
                if (callStack.Count==0) {
                    return ThreadStatus.FINISHED;
                }
            }
            var opcode = CurrentProcedure.Current;
            Deb.storeOpcode(opcode);
            Deb.logmisc("Current Opcode", opcode.Label, opcode);
            opcode.Execute(this);

            switch(opcode.Code){
            case ByteCode.WAIT:
                return ThreadStatus.WAIT;
            case ByteCode.RETURN:
                Deb.logmisc("Removing ProcedureExec");
                callStack.Pop();
                if (callStack.Count==0) {
                    return ThreadStatus.FINISHED;
                }
                break;
            }

            return ThreadStatus.OK;
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

        public void Terminate()
        {
            isTerminated=true;
        }

        internal void Wait(double arg)
        {
            timeToWaitInMilliseconds=Convert.ToInt64(arg)*1000;
            waitWatch.Start();
        }

        // This has to increment the GlobalInstructionPointer
        // to prevent the game from locking up when all threads are sleeping.
        bool isWaiting(out ThreadStatus threadStatus)
        {
            if (timeToWaitInMilliseconds>0 &&
                waitWatch.ElapsedMilliseconds>timeToWaitInMilliseconds) {
                waitWatch.Reset();
                // Count this as an instruction so that if all threads are waiting
                // we can still eventually return to allow the fixedupdate
                if (GlobalInstructionCounter.Continue()) {
                    threadStatus=ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
                } else {
                    threadStatus=ThreadStatus.WAIT;
                }
                return true;
            }
            threadStatus=ThreadStatus.OK;
            return false;
        }

        KOSThread IExec.Thread => this;

        KOSProcess IExec.Process => Process;

        ArgumentStack IExec.Stack => Stack;

        VariableStore IExec.Store => CurrentStore;

        SafeSharedObjects IExec.Shared => Process.ProcessManager.shared;

        public object PopValue(bool barewordOkay = false)
        {
            var retval = Stack.Pop();
            Deb.logmisc("Getting value of", retval);
            var retval2 = CurrentStore.GetValue(retval, barewordOkay);
            Deb.logmisc("Got value of", retval2);
            return retval2;
        }
    }
}
