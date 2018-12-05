using kOS.Safe.Compilation;
using System.Collections.Generic;
// i'm using this alias because this project redefines 'Stack'
// and I don't want that to lead to weird behavior
using coll = System.Collections.Generic;
using System;
using kOS.Safe.Execution;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        FINISHED,
        GLOBAL_INSTRUCTION_LIMIT,
        WAIT,
        ERROR,
        TERMINATED,
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

        internal ProcessManager ProcessManager { get; }
        /// <summary>
        /// Stores a set of threads. Threads in this set will be added into
        /// the threadStack when it is done running.
        /// </summary>
        readonly HashSet<KOSThread> threadSet = new HashSet<KOSThread>();
        /// <summary>
        /// Works similar to threadSet.
        /// </summary>
        readonly HashSet<KOSThread> triggerSet = new HashSet<KOSThread>();

        /// <summary>
        /// Stack to be executed until it's empty. The stack effectively keeps
        /// track of what was last executed even past an update. When it's empty it will
        /// eventually get filled up by the threadSet.
        /// </summary>
        readonly coll.Stack<KOSThread> threadStack = new coll.Stack<KOSThread>();
        /// <summary cref="ThreadStatus">
        /// Works similar to threadStack.
        /// </summary>
        readonly coll.Stack<KOSThread> triggerStack = new coll.Stack<KOSThread>();

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;
        }

        public ProcessStatus Status { get; set; } = ProcessStatus.OK;

        public void Execute()
		{
            Deb.EnqueueExec("Process Execute. Threads", threadSet.Count);
            Deb.EnqueueExec("Process Execute. Triggers", triggerSet.Count);

            switch (Status) {
            case ProcessStatus.GLOBAL_INSTRUCTION_LIMIT:
            case ProcessStatus.WAIT:
                Status = ProcessStatus.OK;
                break;
            case ProcessStatus.TERMINATED:
            case ProcessStatus.ERROR:
            case ProcessStatus.FINISHED:
                return;
            }

            FillTriggerStackIfEmpty();
            ExecuteThreads(triggerStack);

            switch (Status) {
            case ProcessStatus.WAIT:
                Status = ProcessStatus.OK;
                break;
            case ProcessStatus.GLOBAL_INSTRUCTION_LIMIT:
            case ProcessStatus.TERMINATED:
            case ProcessStatus.ERROR:
            case ProcessStatus.FINISHED:
                return;
            }

            // execute all threads until GLOBAL_INSTRUCTION_LIMIT is
            // exceeded
            while (Status == ProcessStatus.OK){
                FillThreadStackIfEmpty();
                ExecuteThreads(threadStack);

                if (threadSet.Count == 0) {
                    Status = ProcessStatus.FINISHED;
                }
            }

        }

        void ExecuteThreads(coll.Stack<KOSThread> stack){
            if (stack.Count == 0) return;

            bool allThreadsWaiting = true;
            while (stack.Count>0) {
                var currentThread = stack.Peek();
                currentThread.Execute();
                var status = currentThread.Status;

                if(status!=ThreadStatus.WAIT){
                    allThreadsWaiting=false;
                }

                switch (status) {

                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                case ThreadStatus.WAIT:
                    stack.Pop();
                    break;
                // If the global limit was reached, return to the current
                // thread after the update is over. (Don't pop the current
                // thread/trigger from its stack)
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
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

            if(allThreadsWaiting){
                Status=ProcessStatus.WAIT;
            }
        }

        /// <summary>
        /// Remove the thread/trigger/systemtrigger properly.
        /// </summary>
        /// <param name="thread">Thread.</param>
        void RemoveThread(KOSThread thread) {
            if(thread is SystemTrigger) {
                RemoveSystemTrigger(((SystemTrigger)thread).Name);
            }
            if(thread is KOSTrigger) {
                RemoveTrigger((KOSTrigger)thread);
            }
            threadSet.Remove(thread);
        }

        /// <summary>
        /// Fills the trigger stack with the contents of the triggerSet 
        /// if empty.
        /// </summary>
        void FillTriggerStackIfEmpty(){
            if (triggerStack.Count==0) {
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
            if (systemTrigger==null)
                throw new Exception(
                    "SystemTriggers passed to AddSystemTrigger cannot be null");
            if(SystemTriggerMap.ContainsKey(systemTrigger.Name)){
                RemoveSystemTrigger(systemTrigger.Name);
            }
            SystemTriggerMap.Add(systemTrigger.Name, systemTrigger);
            triggerSet.Add(systemTrigger);
        }

        /// <summary>
        /// Removes the system trigger.
        /// </summary>
        /// <returns><c>true</c>, if system trigger was removed, <c>false</c> otherwise.</returns>
        /// <param name="name">Name.</param>
        public bool RemoveSystemTrigger(string name){
            if(SystemTriggerMap.TryGetValue(name, out SystemTrigger systemTrigger)){
                SystemTriggerMap.Remove(name);
                return triggerSet.Remove(systemTrigger);
            }
            return false;
        }

        internal void RemoveTrigger(KOSTrigger kOSTrigger) {
            triggerSet.Remove(kOSTrigger);
        }
    }
}
