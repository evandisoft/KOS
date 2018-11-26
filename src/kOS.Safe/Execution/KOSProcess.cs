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
        QUEUE_FINISHED
    }


    public class KOSProcess {
        internal ProcessManager ProcessManager { get; }
        readonly HashSet<KOSThread> threadSet = new HashSet<KOSThread>();
        readonly HashSet<KOSThread> triggerSet = new HashSet<KOSThread>();

        readonly coll.Stack<KOSThread> runStack= new coll.Stack<KOSThread>();


        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;
        }


        public ProcessStatus Execute()
		{
            Deb.logmisc("Process Execute. Threads", threadSet.Count);
            Deb.logmisc("Process Execute. Triggers", triggerSet.Count);
            if (threadSet.Count==0){ return ProcessStatus.FINISHED; }
            if(runStack.Count==0){
                foreach (var thread in threadSet) {
                    runStack.Push(thread);
                }
                foreach (var thread in triggerSet) {
                    runStack.Push(thread);
                }
            }

            var status = ProcessStatus.OK;
            while(status==ProcessStatus.OK){
                status = ExecuteThread(runStack.Peek());
            }

            return ProcessStatus.OK;
        }


        ProcessStatus ExecuteThread(KOSThread thread){
            var status = thread.Execute();
            switch (status) {

            // If the thread limit is reached, start executing the next
            // thread.
            case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                runStack.Pop();
                break;
            // If the global limit is reached, return to the current
            // thread after the update is over. (do not cycle this 
            // queue this time.)
            case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                return ProcessStatus.OK;
            // if this thread has an error, or is finished, remove
            // it
            case ThreadStatus.TERMINATED:
            case ThreadStatus.ERROR:
            case ThreadStatus.FINISHED:
                RemoveThread(thread);
                if(threadSet.Count==0){
                    return ProcessStatus.FINISHED;
                }
                runStack.Pop();
                break;
            default:
                runStack.Pop();
                break;
            }
            // tell the main loop that this queue has been fully traversed
            return ProcessStatus.QUEUE_FINISHED;
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
