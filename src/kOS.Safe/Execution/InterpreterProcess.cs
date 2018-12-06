using System;
namespace kOS.Safe.Execution {
    public class InterpreterProcess : KOSProcess {
        public InterpreterProcess(ProcessManager processManager) : base(processManager) {
        }

        protected override void SetFinishedStatusIfFinished() {
            if(threadSet.Count==0 && triggerSet.Count == 0) {
                Status = ProcessStatus.FINISHED;
            }
        }
    }
}
