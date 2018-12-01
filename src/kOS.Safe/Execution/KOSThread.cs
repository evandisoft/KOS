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

    /// <summary>
    /// KOSThread.
    /// This thread is the center of all computation.
    /// It passes itself to OpcodeExecute as an IExec.
    /// It manages ProcedureExec instances on a stack.
    /// It uses ProcedureExec as an IEnumerator<Opcode>
    /// to get the opcode to execute next.
    /// </summary>
    public class KOSThread:IExec {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;
        bool isTerminated = false;

        internal KOSProcess Process { get; }
        internal ArgumentStack Stack { get; }

        /// <summary>
        /// Gets the current procedure.
        /// </summary>
        /// <value>The current procedure.</value>
        ProcedureCall CurrentProcedure => callStack.Peek();
        VariableStore CurrentStore => CurrentProcedure.Store;

        readonly coll.Stack<ProcedureCall> callStack = new coll.Stack<ProcedureCall>();

        /// <summary>
        /// The thread instruction counter.
        /// This counter keeps track of the threads own execution limit
        /// TODO: Currently this is just set to the normal instructions
        /// per update limit. I must give this a separate implementation
        /// so that there can be a different limit per thread
        /// </summary>
        internal InstructionCounter ThreadInstructionCounter = new InstructionCounter();
        /// <summary>
        /// The global instruction counter.
        /// This counter keeps track of the total limit on
        /// instructions per update.
        /// </summary>
        internal InstructionCounter GlobalInstructionCounter;
        Stopwatch waitWatch = new Stopwatch();
        long timeToWaitInMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:kOS.Safe.KOSThread"/> class.
        /// </summary>
        /// <param name="process">Process.</param>
        public KOSThread(KOSProcess process)
        {
            Process=process;
            Stack=new ArgumentStack();
            GlobalInstructionCounter=Process.ProcessManager.GlobalInstructionCounter;
        }

        /// <summary>
        /// Execute this instance.
        /// </summary>
        public ThreadStatus Execute()
        {
            Deb.logmisc("Executing thread", ID, "ProcedureExecs", callStack.Count);

            if(IsWaiting()){
                return ThreadStatus.WAIT;
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

        /// <summary>
        /// Executes the current procedure's current opcode.
        /// Checks the global instruction pointer and this threads
        /// instruction pointer.
        /// </summary>
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
            // For IEnumerators, Must call MoveNext prior to 
            // any call to Current
            if (!CurrentProcedure.MoveNext()) {
                return PopStackAndReturnFinishedIfEmpty();
            }

            var opcode = CurrentProcedure.Current;
            Deb.storeOpcode(opcode);
            Deb.logmisc("Current Opcode", opcode.Label, opcode);
            opcode.Execute(this);

            switch(opcode.Code){
            case ByteCode.WAIT:
                return ThreadStatus.WAIT;
            case ByteCode.RETURN:
                return PopStackAndReturnFinishedIfEmpty();
            }


            return ThreadStatus.OK;
        }

        /// <summary>
        /// Pops the callStack. If it is empty, returns ThreadStatus.FINISHED,
        /// otherwise returns ThreadStatus.OK.
        /// </summary>
        ThreadStatus PopStackAndReturnFinishedIfEmpty(){
            Deb.logmisc("Removing ProcedureExec");
            callStack.Pop();
            if (callStack.Count==0) {
                return ThreadStatus.FINISHED;
            }
            return ThreadStatus.OK;
        }


        /// <summary>
        /// Call the specified procedure.
        /// Create a new ProcedureExec, and add it to the stack, 
        /// to be executed next time this thread runs.
        /// Called by OpcodeCall.Execute
        /// Used only for calls made within this thread.
        /// Because it requires that arguments were already pushed onto
        /// the stack that is local to this thread.
        /// </summary>
        /// <param name="procedure">Procedure.</param>
        public void Call(Procedure procedure)
        {
            ProcedureCall call = procedure.Call(this);
            callStack.Push(call);
        }


        /// <summary>
        /// Calls the procedure with arguments.
        /// This is used for 'run' because it passes arguments explicitly.
        /// </summary>
        /// <param name="procedure">Procedure.</param>
        /// <param name="args">Arguments.</param>
        public void CallWithArgs(Procedure procedure, coll.List<object> args)
        {
            ProcedureCall call = procedure.Call(this);
            callStack.Push(call);
            Stack.Push(new KOSArgMarkerType());
            for (int i = args.Count-1;i>=0;i--) {
                Stack.Push(args[i]);
            }
        }

        /// <summary>
        /// Terminate this thread.
        /// </summary>
        public void Terminate()
        {
            isTerminated=true;
        }

        /// <summary>
        /// Make this thread wait the specified number of seconds.
        /// </summary>
        /// <param name="seconds">Argument.</param>
        internal void Wait(double seconds)
        {
            timeToWaitInMilliseconds=Convert.ToInt64(seconds*1000);
            waitWatch.Start();
        }


        /// <summary>
        /// Returns true if the thread is Waiting.
        /// </summary>
        bool IsWaiting()
        {
            if (timeToWaitInMilliseconds>0){
                if(timeToWaitInMilliseconds>waitWatch.ElapsedMilliseconds){
                    Deb.logmisc("Thread", ID, "is waiting for",
                           timeToWaitInMilliseconds-waitWatch.ElapsedMilliseconds,
                           "more milliseconds");
                    return true;
                }
                Deb.logmisc("Thread", ID, "is no longer waiting.");
                timeToWaitInMilliseconds=0; waitWatch.Reset();
            }
            return false;
        }

        // Implementation of IExec
        SafeSharedObjects IExec.Shared => Process.ProcessManager.shared;
        ProcessManager IExec.ProcessManager => Process.ProcessManager;
        KOSProcess IExec.Process => Process;
        KOSThread IExec.Thread => this;
        ArgumentStack IExec.Stack => Stack;
        VariableStore IExec.Store => CurrentStore;

        /// <summary>
        /// Pops the value from the Stack, looking it up in the Store if it's
        /// a variable.
        /// </summary>
        /// <returns>The value.</returns>
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
