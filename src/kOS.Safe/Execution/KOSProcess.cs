using kOS.Safe.Compilation;
using System.Collections.Generic;
// i'm using this alias because this project redefines 'Stack'
// and I don't want that to lead to weird behavior
using coll=System.Collections.Generic;
using System;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        FINISHED,
        GLOBAL_INSTRUCTION_LIMIT,
        STACK_EMPTY,
        WAIT,
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
        /// <summary>
        /// Works similar to threadStack
        /// </summary>
        readonly coll.Stack<KOSThread> triggerStack = new coll.Stack<KOSThread>();

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;
        }

        public ProcessStatus Execute()
		{
            Deb.logmisc("Process Execute. Threads", threadSet.Count);
            Deb.logmisc("Process Execute. Triggers", triggerSet.Count);
            if (threadSet.Count==0){ return ProcessStatus.FINISHED; }
            ProcessStatus status;

            FillTriggerStackIfEmpty();
            status = ExecuteThreads(triggerStack);

            switch(status){
            case ProcessStatus.OK:
                break;
                // We don't care if all the triggers are waiting.
                // Only the threads run in a loop until GLOBAL_INSTRUCTION_LIMIT
                // is reached.
            case ProcessStatus.WAIT:
                status=ProcessStatus.OK;
                break;
            default:
                return status;
            }

            // execute all threads until GLOBAL_INSTRUCTION_LIMIT is
            // exceeded
            while(status==ProcessStatus.OK){
                FillThreadStackIfEmpty();
                status = ExecuteThreads(threadStack);
                // If there are no normal threads left, then end
                // this process
                if (threadSet.Count==0) {
                    return ProcessStatus.FINISHED;
                }
            }

            return status;
        }

        ProcessStatus ExecuteThreads(coll.Stack<KOSThread> stack){
            if (stack.Count==0) return ProcessStatus.OK;
            // Keep track of whether all the threads were waiting.
            // If they were all waiting, then effectively the process
            // is waiting.
            bool allThreadsWaiting = true;
            while (stack.Count>0) {
                var currentThread = stack.Peek();
                var status = currentThread.Execute();

                if(status!=ThreadStatus.WAIT){
                    allThreadsWaiting=false;
                }

                switch (status) {

                // If the thread limit was reached, start executing the next
                // thread.
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    stack.Pop();
                    break;
                // If the thread is waiting, start executing the next
                // thread.
                case ThreadStatus.WAIT:
                    stack.Pop();
                    break;
                // If the global limit was reached, return to the current
                // thread after the update is over. (Don't pop the current
                // thread/trigger from its stack)
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                    return ProcessStatus.GLOBAL_INSTRUCTION_LIMIT;
                // if this thread has an error, or has been terminated, remove
                // it
                case ThreadStatus.TERMINATED:
                case ThreadStatus.ERROR:
                    if (currentThread is SystemTrigger) {
                        RemoveSystemTrigger(((SystemTrigger)currentThread).Name);
                    }else{
                        RemoveThread(currentThread);
                    }

                    stack.Pop();
                    break;
                case ThreadStatus.FINISHED:

                    stack.Pop();
                    if (currentThread is SystemTrigger){
                        RemoveSystemTrigger(((SystemTrigger)currentThread).Name);
                        throw new Exception(
                            "SystemTrigger thread should not have 'FINISHED'.");
                    }
                    RemoveThread(currentThread);

                    break;
                default:
                    // Pop this thread off the current stack so that it will not
                    // be executed again until the stack is repopulated
                    stack.Pop();
                    break;
                }
            }

            if(allThreadsWaiting){
                return ProcessStatus.WAIT;
            }
            return ProcessStatus.OK;
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

        public void RemoveThread(KOSThread thread){
            threadSet.Remove(thread);
            triggerSet.Remove(thread);
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
        /// Adds the system trigger. If a system Trigger of the same
        /// name already exists, it first removes that trigger.
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

        public bool RemoveSystemTrigger(string name){
            if(SystemTriggerMap.TryGetValue(name, out SystemTrigger systemTrigger)){
                SystemTriggerMap.Remove(name);
                return triggerSet.Remove(systemTrigger);
            }
            return false;
        }
    }
}
