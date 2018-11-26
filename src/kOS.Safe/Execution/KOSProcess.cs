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

    // adds a "default" sentinel value (will only work with types that
    // can be null)
    // in order to keep track of when the entire queue has been cycled through
    public class SentinelQueue<T>:Queue<T>{
        public SentinelQueue(){
            base.Enqueue(default(T));
        }
        public void Cycle(){
            base.Enqueue(base.Dequeue());
        }
        public bool IsDone(){
            return Peek()==null;
        }
        // prevent the user from adding a null;
        new public void Enqueue(T item){
            if(item==null){
                throw new Exception("Sentinel Queues cannot contain nulls");
            }
            base.Enqueue(item);
        }
        // prevent the user from removing the null;
        new public T Dequeue(){
            if(IsDone()){
                return default(T);
            }
            return base.Dequeue();
        }
        // ignore the 'null' that is always there when calculating Count
        new public int Count { get => base.Count-1; }
    }

    // This class is just a queue with Cycle functionality
    public class CycleQueue<T> : Queue<T> {
        public void Cycle()
        {
            Enqueue(Dequeue());
        }
    }

    class ThreadQueue:SentinelQueue<KOSThread>{
        public string Name { get; set; }
    }

    // This implements an execution strategy whereby we leave a ProcedureExec
    // after it hits the instructions-per-update limit, and come back to it
    // when the update is over. Then we also limit each thread with a 
    // thread instruction limit to ensure that it doesn't hog all the 
    // execution time.
    // When a thread enters a waiting mode or hits the thread instruction 
    // limit, we move on to the next thread. The usage of queues here 
    // allows us to remember which thread was supposed to execute next 
    // whenever we are forced to exit to allow an update.
    // This process will remain alive until there are no more threads
    // remaining (trigger threads do not count).
    // Threads end with the "FINISHED" status or the "ERROR"
    // status. Errors in only one thread do not end the process. They just
    // end that particular thread.
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
            threadQueue.Name="ThreadQueue";
            triggerQueue.Name="TriggerQueue";
        }

        // alternate between the threadQueue and triggerQueue. For each
        // queue, cycle through it until it is done. Then switch to the other
        // queue.
        // Using queues to remember whether we are processing the threads 
        // or triggers queue and also which particular thread or trigger 
        // we are processing in that queue so that when we return
        // after the update we can continue exactly where we left off.
        public ProcessStatus Execute()
		{
            Deb.logmisc("Process Execute. Threads", threadQueue.Count);
            Deb.logmisc("Process Execute. Triggers", triggerQueue.Count);
            if (threadQueue.Count==0){ return ProcessStatus.FINISHED; }

            var status = ProcessStatus.OK;
            while(status==ProcessStatus.OK){
                var currentQueue = queueQueue.Peek();
                status = ExecuteThreadQueue(currentQueue);
                Deb.logmisc("status", status);
                if (status==ProcessStatus.QUEUE_FINISHED) {
                    // if we've dequeued all the threads from
                    // the threadQueue we will signal that this Process
                    // is done. We do not want to keep a process
                    // alive just to run a bunch of triggers.
                    if (threadQueue.Count==0) {
                        return ProcessStatus.FINISHED;
                    }
                    queueQueue.Cycle(); // switch to the other queue
                    status=ProcessStatus.OK;
                }
            }
            return status;
        }

        // Cycle through the threads in the queue, executing each one
        // until you reach the sentinel value of 'null' (IsDone()==true) 
        // that marks a complete traversal.
        ProcessStatus ExecuteThreadQueue(ThreadQueue queue){
            while (!queue.IsDone()){
                var currentThread = queue.Peek();
                var status = currentThread.Execute();
                switch (status) {

                // If the thread limit is reached, start executing the next
                // thread.
                case ThreadStatus.THREAD_INSTRUCTION_LIMIT:
                    break;
                // If the global limit is reached, return to the current
                // thread after the update is over. (do not cycle this 
                // queue this time.)
                case ThreadStatus.GLOBAL_INSTRUCTION_LIMIT:
                    return ProcessStatus.OK;
                // if this thread has an error, or is finished, remove
                // it
                case ThreadStatus.ERROR:
                case ThreadStatus.FINISHED:
                    queue.Dequeue();
                    break;
                }
                // put the current thread into the back of the queue
                queue.Cycle(); 
            }
            // put the null sentinel to the back of the queue
            queue.Cycle(); 
            // tell the main loop that this queue has been fully traversed
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
