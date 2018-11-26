﻿using kOS.Safe.Compilation;
using System.Collections.Generic;
using System;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        FINISHED,
        QUEUE_FINISHED
    }

    // adds a "default" sentinel value (only makes sense for reference types)
    // in order to keep track of when the entire queue has been cycled through
    public class SentinelQueue<T>{
        readonly Queue<T> baseQueue = new Queue<T>();

        public SentinelQueue(){
            baseQueue.Enqueue(default);
        }
        public void Cycle(){
            baseQueue.Enqueue(baseQueue.Dequeue());
        }
        public bool IsDone(){
            return baseQueue.Peek()==default;
        }
        public T Peek(){
            return baseQueue.Peek();
        }
        // prevent the user from adding a null;
        public void Enqueue(T item){
            if(item==default){
                throw new Exception("Sentinel Queues cannot contain nulls");
            }
            baseQueue.Enqueue(item);
        }
        // prevent the user from removing the null;
        public T Dequeue(){
            if(IsDone()){
                return default;
            }
            return baseQueue.Dequeue();
        }
        // ignore the 'null' that is always there when calculating Count
        public int Count { get => baseQueue.Count-1; }
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
    // This KOSProcess will remain alive until there are no more threads
    // remaining (trigger threads do not count).
    // Threads end with the "FINISHED" status or the "ERROR"
    // status. Errors in only one thread do not end the process. They just
    // end that particular thread.
    public class KOSProcess {
        internal ProcessManager ProcessManager { get; }
        readonly ThreadQueue threadQueue = new ThreadQueue();
        readonly ThreadQueue triggerQueue = new ThreadQueue();
        readonly CycleQueue<ThreadQueue> queueCycler = new CycleQueue<ThreadQueue>();
        // threadMap provides access to threads/triggers via their ID in case
        // there is some need to keep track of a thread/trigger this way
        internal readonly Dictionary<long, KOSThread> threadMap = 
            new Dictionary<long, KOSThread>();

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;

            queueCycler.Enqueue(triggerQueue);
            queueCycler.Enqueue(threadQueue);
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
                var currentQueue = queueCycler.Peek();
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
                    queueCycler.Cycle(); // switch to the other queue
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
                    queue.Cycle();
                    continue;
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
                    threadMap.Remove(currentThread.ID);
                    queue.Dequeue();
                    continue;
                default:
                    // by default move to the next thread
                    queue.Cycle();
                    break;
                }
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
            threadMap.Add(thread.ID, thread);
            threadQueue.Enqueue(thread);
        }
        public void AddTrigger(KOSThread thread)
        {
            if (thread==null)
                throw new Exception("Triggers passed to AddTrigger cannot be null");
            threadMap.Add(thread.ID, thread);
            triggerQueue.Enqueue(thread);
        }
    }
}
