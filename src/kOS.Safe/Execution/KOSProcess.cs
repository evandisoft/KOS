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
    }

    /// <summary>
    /// KOSProcess.
    /// </summary>
    public class KOSProcess {
        /// <summary>
        /// Maps SystemTrigger names to SystemTriggers
        /// </summary>
        readonly Dictionary<string, SystemTrigger> SystemTriggerMap = 
            new Dictionary<string, SystemTrigger>();

        internal ProcessManager ProcessManager { get; }
        /// <summary>
        /// Stores a set of threads. Threads in this set will be added into
        /// the threadStack when it is done running.
        /// </summary>
        readonly HashSet<KOSThread> threadSet = new HashSet<KOSThread>();
        readonly HashSet<KOSThread> triggerSet = new HashSet<KOSThread>();

        /// <summary>
        /// Stack to be executed until it's empty. The stack effectively keeps
        /// track of what was last executed even past an update. When it's empty it will
        /// eventually get filled up by the threadSet.
        /// </summary>
        readonly coll.Stack<KOSThread> threadStack = new coll.Stack<KOSThread>();
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
            while (stack.Count>0) {
                var currentThread = stack.Peek();
                var status = currentThread.Execute();

                switch (status) {

                // If the thread limit was reached, start executing the next
                // thread.
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    break;
                case ThreadStatus.WAIT:
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
                    RemoveThread(currentThread);

                    break;
                case ThreadStatus.FINISHED:
                    if(currentThread is SystemTrigger){
                        throw new Exception(
                            "SystemTrigger thread should not have 'FINISHED'.");
                    }
                    RemoveThread(currentThread);

                    break;
                }

                // stop executing this thread until the next time
                // the stack is repopulated
                stack.Pop();
                return ProcessStatus.OK;
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

        public void AddSystemTrigger(SystemTrigger systemTrigger){
            if (systemTrigger==null)
                throw new Exception(
                    "SystemTriggers passed to AddSystemTrigger cannot be null");
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
