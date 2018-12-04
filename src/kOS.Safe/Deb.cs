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
        }
        const string logBasename = "Logs/kOS/";
        static Dictionary<QueueLogType,Queue<object[]>>
            logQueueDictionary=new Dictionary<QueueLogType,Queue<object[]>>();
        static public bool loggingEnabled = false;
        //static Dictionary<QueueLogType, bool>
            //enabledDictionary= new Dictionary<QueueLogType, bool>();
        static public int LogLength=10000;
        static void log(QueueLogType logType, Queue<object[]> objectArrays) {
            rawlog("Logging " + logType + " to disk, of size "+objectArrays.Count);
            string logFilename = logname(logType);
            foreach(var objArray in objectArrays) {
                File.AppendAllText(logFilename, ToString(objArray)+"\n");
            }
        }
        static string logname(QueueLogType queueLogType) {
            return logBasename + queueLogType.ToString().ToLower() + ".log";
        }
        static public void clearLogs() {
            foreach(var logType in logQueueDictionary.Keys) {
                File.WriteAllText(logname(logType), "");
            }
        }
        static public void clearQueues() {
            foreach (var queue in logQueueDictionary.Values) {
                queue.Clear();
            }
        }
        static void store(QueueLogType logType,object[] obj){
            if (loggingEnabled) {
                Queue<object[]> queue=new Queue<object[]>();
                if (logQueueDictionary.TryGetValue(logType, out Queue<object[]> q)) {
                    queue = q;
                } else {
                    logQueueDictionary[logType] = queue;
                }
                if (queue.Count >= LogLength) {
                    queue.Dequeue();
                }
                queue.Enqueue(obj);
            }
        }
        static public void logall(){
            rawlog("Logging all. There are "+logQueueDictionary.Count+" log queues");
            foreach(var logItem in logQueueDictionary) {
                log(logItem.Key,logItem.Value);
            }
        }

        const string rawlogfilename= logBasename + "raw.log";
        static public void rawlog(object obj) {
            File.AppendAllText(rawlogfilename, obj+"\n");
        }
        static public void clearrawlog() {
            File.WriteAllText(rawlogfilename, "");
        }
        static string ToString(object obj) {
            if (obj is string) {
                return ((string)obj);
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
                stringBuilder.Append(",");
            }
            return stringBuilder.ToString();
        }
        static public void storeOpcode(params object[] objs) {
            store(QueueLogType.OPCODE, objs);
        }
        static public void storeException(params object[] objs) {
            store(QueueLogType.EXCEPTION, objs);
        }
        static public void storeExec(params object[] objs) {
            store(QueueLogType.EXEC, objs);
        }
        static public void storeCompile(params object[] objs) {
            store(QueueLogType.COMPILE, objs);
        }
        //static public void enable(QueueLogType logType) {
        //    enabledDictionary[logType] = true;
        //}
        //static public void disable(QueueLogType logType) {
        //    enabledDictionary[logType] = false;
        //}

        //static public void enableAll() {
        //    rawlog("enabling all");
        //    foreach (var logType in enabledDictionary.Keys) {
        //        enabledDictionary[logType] = true;
        //    }
        //}
        //static public void disableAll() {
        //    rawlog("disabling all");
        //    foreach (var logType in enabledDictionary.Keys) {
        //        enabledDictionary[logType] = false;
        //    }
        //}
    }
}
