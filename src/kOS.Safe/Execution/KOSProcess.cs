using kOS.Safe.Compilation;
using System.Collections.Generic;
// i'm using this alias because this project redefines 'Stack'
// and I don't want that to lead to weird behavior
using coll = System.Collections.Generic;
using System;
using kOS.Safe.Execution;
using kOS.Safe.Binding;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        FINISHED,
        GLOBAL_INSTRUCTION_LIMIT,
        ERROR,
        TERMINATED,
        SHUTDOWN,
        INTERRUPTED,
    }

    /// <summary>
    /// KOSProcess.
    /// This manages triggers, threads, and systemtriggers. System triggers and
    /// regular triggers are stored in triggerSet, and threads in threadSet.
    /// Triggers are added from triggerSet to triggerStack when that stack is empty.
    /// Threads are added from threadSet to threadStack when that stack is empty.
    /// 
    /// For these stacks, the threads/triggers in them are executed and then
    /// removed from their stack if their execution is suspended via WAIT or
    /// THREAD_INSTRUCTION_LIMIT. If the thread/trigger reaches 
    /// GLOBAL_INSTRUCTION_LIMIT, it remains on its stack to be executed on 
    /// the next FixedUpdate. If the thread/trigger has an ERROR or is FINISHED, 
    /// the thread/trigger is removed from the stack and from the corresponding Set.
    /// 
    /// The triggerStack is always executed first on each FixedUpdate. If it is empty
    /// because all triggers completed in the last update, it will be filled again with
    /// the contents of the triggerSet at the beginning of the current update. and
    /// then executed. If it did not complete in the last update, it will continue
    /// where it left off and then once done it will move on to the threadStack.
    /// 
    /// The threadStack will not return to the FixedUpdate until GLOBAL_INSTRUCTION_LIMIT
    /// is reached. It is executed in a loop and refilled if ever empty.
    /// </summary>
    public class KOSProcess {
        static long IDCounter = 0;
        public readonly long ID = IDCounter++;

        /// <summary>
        /// Maps SystemTrigger names to SystemTriggers, so that we can remove
        /// them later.
        /// </summary>
        readonly Dictionary<string, SystemTrigger> SystemTriggerMap = 
            new Dictionary<string, SystemTrigger>();

        protected internal ProcessManager ProcessManager { get; }
        /// <summary>
        /// Stores a set of threads. Threads in this set will be added into
        /// the threadStack when it is empty.
        /// </summary>
        protected readonly HashSet<KOSThread> threadSet = new HashSet<KOSThread>();
        /// <summary>
        /// Works similar to threadSet.
        /// </summary>
        protected readonly HashSet<KOSThread> triggerSet = new HashSet<KOSThread>();

        /// <summary>
        /// Stack to be executed until it's empty. The stack effectively keeps
        /// track of what was last executed even past an update. When it's empty it will
        /// eventually get filled up by the threadSet.
        /// </summary>
        protected readonly coll.Stack<KOSThread> threadStack = new coll.Stack<KOSThread>();
        /// <summary cref="ThreadStatus">
        /// Works similar to threadStack.
        /// </summary>
        protected readonly coll.Stack<KOSThread> triggerStack = new coll.Stack<KOSThread>();


        public FlyByWireManager FlyByWire;

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;
            FlyByWire = new FlyByWireManager(processManager.shared.BindingMgr);
        }

        public ProcessStatus Status { get; set; } = ProcessStatus.OK;

        KOSThread newInterrupt;

        /// <summary>
        /// Stores a thread to be ran as an interrupt in "newInterrupt".
        /// Then it finds the currently running thread and marks it's Status as
        /// "INTERRUPTED".
        /// This will lead to the thread returning with an INTERRUPTED status
        /// which allows ExecuteThreads to place the newInterrupt on the current
        /// stack it is processing, to be processed next.
        /// Examples of these types of interrupts are the GUI callbacks, such
        /// as onlick. When onclick is triggered in the code, via takepress,
        /// the onlick function is to be executed on the next opcode.
        /// This interrupt functionality implements this feature.
        /// This function is not used for normal button presses, as those lead
        /// to the registered onclick Procedure being added into a thread and
        /// placed into the trigger set.
        /// </summary>
        /// <param name="interrupt">Interrupt.</param>
        public void Interrupt(KOSThread interrupt) {
            Deb.EnqueueExec("Calling "+nameof(KOSProcess)+"."+nameof(Interrupt));
            if (newInterrupt != null) {
                throw new Exception(nameof(newInterrupt) + " should have been null");
            }
            newInterrupt = interrupt;
            KOSThread currentThread = CurrentThread();
            if (currentThread != null) {
                currentThread.Status = ThreadStatus.INTERRUPTED;
                return;
            }
            throw new Exception("No current thread to interrupt.");
        }

        KOSThread CurrentThread() {
            if (triggerStack.Count == 0) {
                if (threadStack.Count == 0) {
                    return null;
                } else {
                    return threadStack.Peek();
                }
            }
            return triggerStack.Peek();
        }

        public void Execute()
		{
            Deb.EnqueueExec("Process Execute.");

            if (Status != ProcessStatus.OK) {
                Deb.EnqueueExec("Exiting process",ID, "with status",Status);
                return;
            }

            FillTriggerStackIfEmpty();
            Deb.EnqueueExec("Executing. Triggers", triggerStack.Count);
            ExecuteThreads(triggerStack);

            Deb.EnqueueExec("Finished. Triggers with status", Status);

            if (Status != ProcessStatus.OK) {
                Deb.EnqueueExec("Exiting process", ID, "with status", Status);
                return;
            }

            FillThreadStackIfEmpty();
            Deb.EnqueueExec("Executing. Threads", threadStack.Count);
            ExecuteThreads(threadStack);

            Deb.EnqueueExec("Finished Thread with status", Status);
        }

        /// <summary>
        /// This is overriden by InterpreterProcess to allow it to run
        /// triggers even if all the threads were removed.
        /// 
        /// InterpreterProcess only considers itself finished if both the
        /// triggers and threads were removed.
        /// </summary>
        protected virtual void SetFinishedStatusIfFinished() {
            if (threadSet.Count == 0) {
                Status = ProcessStatus.FINISHED;
            }
        }

        public void Terminate() {
            Status = ProcessStatus.TERMINATED;
        }

        void ExecuteThreads(coll.Stack<KOSThread> stack){
            if (stack.Count == 0) return;

            while (stack.Count>0) {
                var currentThread = stack.Peek();
                currentThread.Execute();
                var status = currentThread.Status;

                switch (status) {

                case ThreadStatus.SHUTDOWN:
                    Status = ProcessStatus.SHUTDOWN;
                    return;
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    currentThread.Status = ThreadStatus.OK;
                    stack.Pop();
                    break;
                case ThreadStatus.WAIT:
                    stack.Pop();
                    break;
                // If the global limit was reached, return to the current
                // thread after the update is over. (Don't pop the current
                // thread/trigger from its stack)
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                    currentThread.Status = ThreadStatus.OK;
                    Status=ProcessStatus.GLOBAL_INSTRUCTION_LIMIT;
                    return;
                case ThreadStatus.TERMINATED:
                    RemoveThread(currentThread);
                    stack.Pop();
                    break;
                case ThreadStatus.ERROR:
                    RemoveThread(currentThread);
                    stack.Pop();
                    break;
                case ThreadStatus.INTERRUPTED:
                    if (newInterrupt == null) {
                        throw new Exception("Though thread had interrupt status, " + nameof(newInterrupt) + " was null");
                    }
                    currentThread.Status = ThreadStatus.OK;
                    stack.Push(newInterrupt);
                    newInterrupt = null;
                    break;
                case ThreadStatus.FINISHED:
                    RemoveThread(currentThread);
                    if (currentThread is SystemTrigger){
                        throw new Exception(
                            "SystemTrigger thread should not have 'FINISHED'.");
                    }
                    stack.Pop();
                    break;
                default:
                    // Pop this thread off the current stack so that it will not
                    // be executed again until the stack is repopulated
                    stack.Pop();
                    break;
                }
            }
        }

        /// <summary>
        /// Remove the thread/trigger/systemtrigger properly.
        /// </summary>
        /// <param name="thread">Thread.</param>
        void RemoveThread(KOSThread thread) {
            switch (thread) {
            case SystemTrigger sys:
                RemoveSystemTrigger(sys.Name);
                break;
            case KOSTrigger trigger:
                RemoveTrigger(trigger);
                break;
            default:
                threadSet.Remove(thread);
                break;
            }
            SetFinishedStatusIfFinished();
        }

        /// <summary>
        /// Fills the trigger stack with the contents of the triggerSet 
        /// if empty.
        /// </summary>
        void FillTriggerStackIfEmpty(){
            if (triggerStack.Count==0) {
                Deb.EnqueueExec("Filling Trigger Stack", triggerSet.Count);
                foreach (var trigger in triggerSet) {
                    triggerStack.Push(trigger);
                }
            }
        }

        /// <summary>
        /// Fills the thread stack with the contents of the threadSet 
        /// if empty.
        /// </summary>
        void FillThreadStackIfEmpty(){
            if (threadStack.Count==0) {
                Deb.EnqueueExec("Filling Thread Stack", threadSet.Count);
                foreach (var thread in threadSet) {
                    threadStack.Push(thread);
                }
            }
        }


        public void AddThread(KOSThread thread)
        {
            if (thread==null)
                throw new Exception("Threads passed to AddThread cannot be null");
            threadSet.Add(thread);
        }

        public void AddTrigger(KOSThread trigger)
        {
            if (trigger==null)
                throw new Exception("Triggers passed to AddTrigger cannot be null");
            triggerSet.Add(trigger);
        }

        /// <summary>
        /// Adds the <paramref name="systemTrigger"/>. If a system Trigger 
        /// of the same name already exists, it first removes that trigger.
        /// </summary>
        /// <param name="systemTrigger">System trigger.</param>
        public void AddSystemTrigger(SystemTrigger systemTrigger){
            Deb.EnqueueExec("Adding System Trigger", systemTrigger.Name);
            if (systemTrigger==null)
                throw new Exception(
                    "SystemTriggers passed to AddSystemTrigger cannot be null");
            if(SystemTriggerMap.ContainsKey(systemTrigger.Name)){
                RemoveSystemTrigger(systemTrigger.Name);
            }
            SystemTriggerMap.Add(systemTrigger.Name, systemTrigger);
            triggerSet.Add(systemTrigger);
            Deb.EnqueueExec("Enabling flybywire", systemTrigger.Name);
            FlyByWire.ToggleFlyByWire(systemTrigger.Name, true);
        }

        /// <summary>
        /// Removes the system trigger.
        /// </summary>
        /// <returns><c>true</c>, if system trigger was removed, <c>false</c> otherwise.</returns>
        /// <param name="name">Name.</param>
        public bool RemoveSystemTrigger(string name){
            Deb.EnqueueExec("Attempting to remove System Trigger", name);
            if(SystemTriggerMap.TryGetValue(name, out SystemTrigger systemTrigger)){
                Deb.EnqueueExec("Trigger found", name);
                SystemTriggerMap.Remove(name);
                FlyByWire.ToggleFlyByWire(systemTrigger.Name, false);
                return triggerSet.Remove(systemTrigger);
            }
            return false;
        }

        internal void RemoveTrigger(KOSTrigger kOSTrigger) {
            triggerSet.Remove(kOSTrigger);
        }

        public void PrepareForDisposal() {
            SystemTriggerMap.Clear();
            threadSet.Clear();
            triggerSet.Clear();
            threadStack.Clear();
            triggerStack.Clear();
            Status = ProcessStatus.TERMINATED;
            FlyByWire.DisableActiveFlyByWire();
        }
    }
}
