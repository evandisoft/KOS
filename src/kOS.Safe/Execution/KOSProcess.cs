using kOS.Safe.Compilation;
using System.Collections.Generic;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        Finished
    }

    public class KOSProcess
    {
        internal ProcessManager ProcessManager { get; }
        readonly List<KOSThread> threads=new List<KOSThread>();

        public KOSProcess(ProcessManager processManager)
        {
            ProcessManager=processManager;
        }

        public void AddThread(KOSThread thread){
            threads.Add(thread);
        }

        public ProcessStatus Execute()
		{
            Deb.logmisc ("Process Execute. Threads", threads.Count);

            if(threads.Count==0){
                return ProcessStatus.Finished;
            }

            for (int i = threads.Count-1; i>= 0;i--) {
                Deb.logmisc ("i", i, "total", threads.Count);
                var status = threads[i].Execute();
                Deb.logmisc("From thread execute. Status", status);

                switch (status) {

                case ThreadStatus.FINISHED:
                    Deb.logmisc("Removing Thread", i);
                    threads.RemoveAt(i);
                    if(threads.Count==0){
                        return ProcessStatus.Finished;
                    }
                    break;

                }
            }

            return ProcessStatus.OK;
        }
    }
}
