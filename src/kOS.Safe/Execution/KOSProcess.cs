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


    public class KOSProcess {
        internal ProcessManager ProcessManager { get; }
        readonly HashSet<KOSThread> threadSet = new HashSet<KOSThread>();
        readonly HashSet<KOSThread> triggerSet = new HashSet<KOSThread>();

        readonly coll.Stack<KOSThread> threadStack= new coll.Stack<KOSThread>();
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

            // execute all threads until GLOBAL_INSTRUCTION_LIMIT
            // exceeded
            while(status==ProcessStatus.OK){
                FillThreadStackIfEmpty();
                status = ExecuteThreads(threadStack);
            }

            return status;
        }

        ProcessStatus ExecuteThreads(coll.Stack<KOSThread> stack){
            while (stack.Count>0) {
                var status = stack.Peek().Execute();

                switch (status) {

                // If the thread limit was reached, start executing the next
                // thread.
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    break;
                // If the global limit was reached, return to the current
                // thread after the update is over. (Don't pop the current
                // thread/trigger from its stack)
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                    return ProcessStatus.GLOBAL_INSTRUCTION_LIMIT;
                // if this thread has an error, or is finished, remove
                // it
                case ThreadStatus.TERMINATED:
                case ThreadStatus.ERROR:
                case ThreadStatus.FINISHED:
                    RemoveThread(stack.Peek());
                    // If all normal threads are done, then end
                    // this process
                    if (threadSet.Count==0) {
                        return ProcessStatus.FINISHED;
                    }
                    break;
                }

                // stop executing this thread until the next time
                // the stack is repopulated
                stack.Pop();
                return ProcessStatus.OK;
            }
            return ProcessStatus.OK;
        }

        public void FillTriggerStackIfEmpty(){
            if (triggerStack.Count==0) {
                foreach (var trigger in triggerSet) {
                    triggerStack.Push(trigger);
                }
            }
        }

        public void FillThreadStackIfEmpty(){
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
    }
}
