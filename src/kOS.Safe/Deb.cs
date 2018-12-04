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
        static bool loggingEnabled = false;
        static public void enableLogging() {
            rawlog("Logging enabled");
            loggingEnabled = true;
        }
        static public void disableLogging() {
            rawlog("Logging disabled");
            loggingEnabled = false;
        }
        static public int LogLength=10000;
        static void log(QueueLogType logType, Queue<object[]> objectArrays) {
            string logFilename = logname(logType);
            rawlog("Logging " + logType + " to "+logFilename+", of size "+objectArrays.Count);

            foreach(var objArray in objectArrays) {
                //rawlog("Attempting Object Array To string");
                string toWrite = ToString(objArray) + "\n";
                //rawlog("writing " + toWrite + " to disk");
                File.AppendAllText(logFilename, toWrite);
            }
            //rawlog("Clearing queue " + logType);
            objectArrays.Clear();
        }
        static string logname(QueueLogType queueLogType) {
            return logBasename + queueLogType.ToString().ToLower() + ".log";
        }
        static public void clearLogs() {
            rawlog("Clearing All Logs");
            foreach(var item in logQueueDictionary) {
                rawlog("Clearing Log " + item.Key);
                File.WriteAllText(logname(item.Key), "");
                //rawlog("Clearing queue " + item.Key);
                item.Value.Clear();
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
            //rawlog("Object Array To string");
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var obj in objs) {
                //rawlog("writing object " + obj+" ToString("+ToString(obj));
                stringBuilder.Append(ToString(obj));
                stringBuilder.Append(",");
            }
            //rawlog("final result " + stringBuilder.ToString());
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
    }
}
