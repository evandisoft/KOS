using kOS.Safe.Compilation;
using System.Collections.Generic;
using System;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        FINISHED,
        QUEUE_FINISHED
    }

    // adds a "default" sentinel value (only intended to be used with "null")
    // in order to keep track of when the entire queue has been cycled through
    public class SentinelQueue<T>:Queue<T>{
        public SentinelQueue(){
            Enqueue(default(T));
        }
        public void Cycle(){
            Enqueue(Dequeue());
        }
        public bool IsDone(){
            return Peek()==null;
        }
    }

    // This class adds the ability to Cycle through the contents
    public class CycleQueue<T> : Queue<T> {
        public void Cycle()
        {
            Enqueue(Dequeue());
        }
    }

    class ThreadQueue:SentinelQueue<KOSThread>{}

    // This implements an execution strategy whereby each thread gets to run
    // for at most MaxInstructionsPerUpdate. When a thread enters a waiting mode
    // or returns for some other reason, we move on to the next thread.
    // The usage of queues here allows us to remember which thread was supposed
    // to execute next if something gets interrupted.
    public class KOSProcess {
        internal ProcessManager ProcessManager { get; }
        readonly ThreadQueue threadQueue = new ThreadQueue();
        readonly ThreadQueue triggerQueue = new ThreadQueue();
        readonly CycleQueue<ThreadQueue> queueQueue = new CycleQueue<ThreadQueue>();

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;

            queueQueue.Enqueue(triggerQueue);
            queueQueue.Enqueue(threadQueue);
        }

        // cycle between the trigger queue and threads queue so that every
        // thread/trigger eventually get's a chance to run.
        public ProcessStatus Execute()
		{
            Deb.logmisc("Process Execute. Threads", threadQueue.Count);
            if(threadQueue.Count==0){return ProcessStatus.QUEUE_FINISHED; }

            var status = ProcessStatus.OK;
            while(status==ProcessStatus.OK){
                var currentQueue = queueQueue.Peek();
                status = ExecuteThreadQueue(currentQueue);
                if (status==ProcessStatus.QUEUE_FINISHED) {
                    // if the thread became empty while processing a queue
                    // we no longer need this process at all.
                    if (threadQueue.Count==0) {
                        return ProcessStatus.FINISHED;
                    }
                    currentQueue.Cycle(); // cycle out the sentinel
                    queueQueue.Cycle(); // switch to the other queue
                    status=ProcessStatus.OK;
                }
            }
            return status;
        }

        // Cycle through the threads in the queue, executing each one
        // until you reach the sentinel value of 'null' (IsDone()==true) 
        // that marks a complete traversal
        ProcessStatus ExecuteThreadQueue(ThreadQueue queue){
            while(!queue.IsDone()){
                var currentThread = queue.Peek();
                var status = currentThread.Execute();
                switch (status) {

                case ThreadStatus.EXECUTION_LIMIT:
                    queue.Cycle();
                    return ProcessStatus.OK;
                case ThreadStatus.ERROR:
                case ThreadStatus.FINISHED:
                    queue.Dequeue();

                    break;
                }

                queue.Cycle();
            }
            return ProcessStatus.QUEUE_FINISHED;
        }


        public void AddThread(KOSThread thread)
        {
            if (thread==null)
                throw new Exception("Threads passed to AddThread cannot be null");
            threadQueue.Enqueue(thread);
        }
    }
}
