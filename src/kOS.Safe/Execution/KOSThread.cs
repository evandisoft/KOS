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
    /// It gets passed to OpcodeExecute as an IExec.
    /// It manages ProcedureCall instances on a stack.
    /// It calls ProcedureCall's Execute function to execute
    /// and return an opcode.
    /// I'm not set on what exactly should be implementing
    /// IExec and getting passed to Opcode.Execute.
    /// Could create some special class just to hold
    /// the references they need and update the references
    /// as we go.
    /// </summary>
    public class KOSThread : IExec {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;

        public SafeSharedObjects Shared { get; }
        public ProcessManager ProcessManager { get; }
        public KOSProcess Process { get; }
        KOSThread IExec.Thread => this;
        public ArgumentStack Stack { get; }
        VariableStore IExec.Store => CurrentProcedure.Store;

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
        public KOSThread(KOSProcess process) {
            Process = process;
            ProcessManager = Process.ProcessManager;
            Stack = new ArgumentStack();
            GlobalInstructionCounter = Process.ProcessManager.GlobalInstructionCounter;
            Shared = Process.ProcessManager.shared;
        }

        public ThreadStatus Status { get; set; } = ThreadStatus.OK;

        /// <summary>
        /// Execute this instance.
        /// </summary>
        public void Execute() {
            Deb.EnqueueExec("Executing thread", ID, nameof(ProcedureCall), callStack.Count);

            ExecuteLoop();

            Deb.EnqueueExec("Exiting thread", ID, "with status", Status);
        }

        void ExecuteLoop() {
            while (true) {
                // These cases are not just to handle the first iteration
                // of this loop, but to also handle any changes in status
                // created by the execution of the Procedure below.
                switch (Status) {
                case ThreadStatus.WAIT:
                    if (StillWaiting()) {
                        return;
                    }
                    StopWaiting();
                    break;
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    Status = ThreadStatus.OK;
                    break;
                case ThreadStatus.FINISHED:
                case ThreadStatus.ERROR:
                case ThreadStatus.TERMINATED:
                    return;
                }

                if (!GlobalInstructionCounter.Continue()) {
                    GlobalInstructionCounter.Reset();
                    Status = ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
                    return;
                }
                if (!ThreadInstructionCounter.Continue()) {
                    ThreadInstructionCounter.Reset();
                    Status = ThreadStatus.THREAD_INSTRUCTION_LIMIT;
                    return;
                }

                try {
                    // This call may lead to the "CurrentProcedure"
                    // being popped off the stack. In this case
                    // any code we add between this call and continue;
                    // that assumes "CurrentProcedure" remains the same as
                    // the one we just executed would be incorrect.
                    // Also if this call leads to the last ProcedureCall
                    // being popped off the stack, CurrentProcedure will
                    // cause an error if you attempt to access it again.
                    CurrentProcedure.ExecuteNextInstruction();
                    continue;
                    // Don't put anything between 
                    // 'CurrentProcedure.ExecuteNextInstruction();'
                    // and
                    // 'continue;'
                } catch (Exception e) {
                    Deb.EnqueueExec(e);
                    Deb.EnqueueException(e);
                    Process.ProcessManager.BreakExecution(false);
                    Status = ThreadStatus.ERROR;
                    throw;
                }
                // Do not put code here. See Above
            }
        }

        bool StillWaiting() {
            if (timeToWaitInMilliseconds > waitWatch.ElapsedMilliseconds) {
                Deb.EnqueueExec(
                    "Thread", ID, "is waiting for",
                    timeToWaitInMilliseconds - waitWatch.ElapsedMilliseconds,
                    "more milliseconds"
                    );
                return true;
            }
            return false;
        }

        void StopWaiting() {
            Deb.EnqueueExec("Thread", ID, "is no longer waiting.");
            timeToWaitInMilliseconds = 0; 
            waitWatch.Reset();
            Status = ThreadStatus.OK;
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
        public void Call(Procedure procedure) {
            ProcedureCall call = procedure.Call(this);
            callStack.Push(call);
        }

        /// <summary>
        /// Calls the procedure with arguments.
        /// This is used for 'run' because it passes arguments explicitly.
        /// </summary>
        /// <param name="procedure">Procedure.</param>
        /// <param name="args">Arguments.</param>
        public void CallWithArgs(Procedure procedure, coll.List<object> args) {
            ProcedureCall call = procedure.Call(this);
            callStack.Push(call);
            Stack.Push(new KOSArgMarkerType());
            for (int i = args.Count - 1;i >= 0;i--) {
                Stack.Push(args[i]);
            }
        }

        /// <summary>
        /// Terminate this thread.
        /// </summary>
        public void Terminate() {
            Status = ThreadStatus.TERMINATED;
        }

        /// <summary>
        /// Make this thread wait the specified number of seconds.
        /// </summary>
        /// <param name="seconds">Argument.</param>
        public void Wait(double seconds) {
            timeToWaitInMilliseconds = Convert.ToInt64(seconds * 1000);
            if (timeToWaitInMilliseconds > 0) {
                waitWatch.Start();
            }
            Status = ThreadStatus.WAIT;
        }

        public void Return() {
            Deb.EnqueueExec("Removing " + nameof(ProcedureCall));
            callStack.Pop();
            if (callStack.Count == 0) {
                Status = ThreadStatus.FINISHED;
                return;
            }
        }


        /// <summary>
        /// Pops the value from the Stack, looking it up in the Store if it's
        /// a variable.
        /// </summary>
        /// <returns>The value.</returns>
        public object PopValue(bool barewordOkay = false) {
            var retval = Stack.Pop();
            Deb.EnqueueExec("Getting value of", retval);
            var retval2 = CurrentStore.GetValue(retval, barewordOkay);
            Deb.EnqueueExec("Got value of", retval2);
            return retval2;
        }


    }
}
