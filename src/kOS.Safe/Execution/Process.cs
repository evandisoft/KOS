using System;
using kOS.Safe.Compilation;
using System.Collections.Generic;

namespace kOS.Safe
{
    public enum ProcessStatus{
        OK,
        Finished
    }

    public class Process
    {
        List<KOSThread> threads=new List<KOSThread>();

        public Process()
        {
        }

        public void AddThread(KOSThread kOSThread){
            threads.Add(kOSThread);
        }

        public ProcessStatus Execute()
		{
            if(threads.Count==0){
                return ProcessStatus.Finished;
            }

            for (int i = threads.Count; i>= 0;i--) {
                var status = threads[i].Execute();

                switch (status) {

                case ThreadStatus.FINISHED:
                    threads.RemoveAt(i);
                    break;

                }
            }

            return ProcessStatus.OK;
        }
    }
}
