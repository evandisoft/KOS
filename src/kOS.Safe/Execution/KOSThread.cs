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
        SHUTDOWN,
        INTERRUPTED,
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
        VariableStore IExec.Store => CurrentProcedureCall.Store;

        // If non-null, this is used to place the return value of the
        // last procedure, if any.
        ReturnCell returnCell;

        /// <summary>
        /// Gets the current procedure.
        /// </summary>
        /// <value>The current procedure.</value>
        ProcedureCall CurrentProcedureCall => callStack.Peek();
        VariableStore CurrentStore => CurrentProcedureCall.Store;

        readonly coll.Stack<ProcedureCall> callStack = new coll.Stack<ProcedureCall>();

        /// <summary>
        /// The global instruction counter.
        /// This counter keeps track of the total limit on
        /// instructions per update.
        /// </summary>
        internal GlobalInstructionCounter GlobalInstructionCounter;
        Stopwatch waitWatch = new Stopwatch();
        long timeToWaitInMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:kOS.Safe.KOSThread"/> class.
        /// </summary>
        /// <param name="process">Process.</param>
        public KOSThread(KOSProcess process,ReturnCell returnCell=null) {
            this.returnCell = returnCell;
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


            switch (Status) {
            case ThreadStatus.WAIT:
                if (StillWaiting()) {
                    return;
                }
                StopWaiting();
                break;
            case ThreadStatus.FINISHED:
            case ThreadStatus.ERROR:
            case ThreadStatus.TERMINATED:
                return;
            }

            ExecuteLoop();

            Deb.EnqueueExec("Exiting thread", ID, "with status", Status);
        }

        void ExecuteLoop() {
            while (true) {
                if (!GlobalInstructionCounter.Continue()) {
                    Status = ThreadStatus.GLOBAL_INSTRUCTION_LIMIT;
                    return;
                }

                try {
                    // This call may lead to the "CurrentProcedureCall"
                    // being popped off the stack. In this case
                    // any code we execute after this call
                    // that assumes "CurrentProcedureCall" remains the same as
                    // the one we just executed would be incorrect.
                    // Also if this call leads to the last ProcedureCall
                    // being popped off the stack, CurrentProcedureCall will
                    // cause an error if you attempt to access it again.
                    CurrentProcedureCall.ExecuteNextInstruction();
                } catch (Exception e) {
                    Deb.EnqueueOpcode(e);
                    Deb.EnqueueExec(e);
                    Deb.EnqueueException(e);
                    Process.ProcessManager.BreakExecution(false);
                    Status = ThreadStatus.ERROR;
                    throw;
                }

                switch (Status) {
                case ThreadStatus.WAIT:
                case ThreadStatus.FINISHED:
                case ThreadStatus.ERROR:
                case ThreadStatus.TERMINATED:
                case ThreadStatus.INTERRUPTED:
                case ThreadStatus.SHUTDOWN:
                    return;
                }
            }
        }

        bool StillWaiting() {
            if (timeToWaitInMilliseconds > waitWatch.ElapsedMilliseconds) {
                Deb.EnqueueExec(
                    "Thread", ID.ToString(), "is waiting for",
                    (timeToWaitInMilliseconds - waitWatch.ElapsedMilliseconds),
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

        /// <summary>
        /// Called by OpcodeReturn.
        /// </summary>
        public void Return() {
            Deb.EnqueueExec("Removing " + nameof(ProcedureCall));
            if (callStack.Count == 1) {
                // if the returnCell is nonnull, set it to the last return value
                if (returnCell != null && Stack.Count > 0) {
                    returnCell.ReturnValue = PopStructureEncapsulated();
                }
                Status = ThreadStatus.FINISHED;
            }
            callStack.Pop();
        }

        public Structure PopStructureEncapsulated(bool barewordOkay = false) {
            return Structure.FromPrimitiveWithAssert(PopValue(barewordOkay));
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
