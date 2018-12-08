using System;
using System.IO;
using kOS.Safe.Utilities;
using kOS.Safe.Compilation;
using kOS.Safe.Execution;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
// file added by evandisoft
namespace kOS.Safe
{
    public class Deb
    {
        public enum QueueLogType{
            COMPILE,
            EXEC,
            EXCEPTION,
            OPCODE,
            BUILD,
        }

        static QueueLogType[] queueLogTypes ={
            QueueLogType.EXEC,QueueLogType.OPCODE,QueueLogType.COMPILE,
            QueueLogType.EXCEPTION, QueueLogType.BUILD };

        const string logBasename = "Logs/kOS/";
        static Dictionary<QueueLogType,Queue<string>>
            logQueueDictionary=new Dictionary<QueueLogType,Queue<string>>();

        static bool loggingEnabled = false;
        static public void EnableLogging() {
            RawLog("Logging enabled");
            loggingEnabled = true;
        }
        static public void DisableLogging() {
            RawLog("Logging disabled");
            loggingEnabled = false;
        }
        static public int LogLength=10000;
        static void Log(QueueLogType logType, Queue<string> strings) {
            string logFilename = Logname(logType);
            RawLog("Logging " + logType + " to "+logFilename+", of size "+strings.Count);
            File.WriteAllText(logFilename, "");
            foreach(var str in strings) {
                string toWrite = str + "\n";
                File.AppendAllText(logFilename, toWrite);
            }
        }
        static string Logname(QueueLogType queueLogType) {
            return logBasename + queueLogType.ToString().ToLower() + ".log";
        }
        static public void ClearLogs() {
            RawLog("Clearing All Logs");
            foreach(var logType in queueLogTypes) {
                if(logQueueDictionary.TryGetValue(logType,out Queue<string> strings)){
                    strings.Clear();
                }
                RawLog("Clearing Log " + logType);
                File.WriteAllText(Logname(logType), "");
            }
        }


        static void Store(QueueLogType logType,object[] objs){
            var logstring = ToString(objs);
            if (loggingEnabled) {
                Queue<string> queue=new Queue<string>();
                if (logQueueDictionary.TryGetValue(logType, out Queue<string> q)) {
                    queue = q;
                } else {
                    logQueueDictionary[logType] = queue;
                }
                if (queue.Count >= LogLength) {
                    queue.Dequeue();
                }
                queue.Enqueue(logstring);
            }
        }
        static public void LogQueues(){
            RawLog("Logging all. There are "+logQueueDictionary.Count+" log queues");
            foreach(var logItem in logQueueDictionary) {
                Log(logItem.Key,logItem.Value);
            }
        }

        const string rawlogfilename= logBasename + "raw.log";
        static public void RawLog(object obj) {
            File.AppendAllText(rawlogfilename, obj+"\n");
        }
        static public void ClearRawLog() {
            File.WriteAllText(rawlogfilename, "");
        }
        static string ToString(object obj) {
            if (obj is string) {
                return obj as string;
            }
            if (obj is Opcode) {
                var opcode = obj as Opcode;
                return opcode.Label + "," + opcode;
            }
            if (obj == null) {
                return "null";
            }
            return obj.ToString();
        }
        static string ToString(object[] objs) {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var obj in objs) {
                stringBuilder.Append(ToString(obj));
                stringBuilder.Append(" ");
            }
            return stringBuilder.ToString();
        }
        static public void EnqueueOpcode(params object[] objs) {
            Store(QueueLogType.OPCODE, objs);
        }
        static public void EnqueueException(params object[] objs) {
            Store(QueueLogType.EXCEPTION, objs);
        }
        static public void EnqueueExec(params object[] objs) {
            Store(QueueLogType.EXEC, objs);
        }
        static public void EnqueueCompile(params object[] objs) {
            Store(QueueLogType.COMPILE, objs);
        }
        static public void EnqueueBuild(params object[] objs) {
            Store(QueueLogType.BUILD, objs);
        }
    }
}
