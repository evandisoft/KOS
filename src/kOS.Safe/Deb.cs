using System;
using System.IO;
using kOS.Safe.Utilities;
// file added by evandisoft
namespace kOS.Safe
{
    public class Deb
    {
        public Deb ()
        {
        }
        static public Boolean verbose = false;
        static public void logall(string filename,params object [] args){
            foreach (var arg in args) {
                if(arg!=null){
                    File.AppendAllText (filename, arg.ToString ());
                } else{
                    File.AppendAllText (filename, "null");
                }

                File.AppendAllText (filename, ", ");
            }

            File.AppendAllText (filename, "\n");
        }
        static string opcodesfilename = "/home/developer/Sync/BigFiles/BigProjects/KOS/opcode.log";
        static string generalLogname = "/home/developer/Sync/BigFiles/BigProjects/KOS/misc.log";
        static public void logopcode(params object [] args){
            logall (opcodesfilename, args);
        }
        static public Boolean setlogmisc = false;
        static public void logmisc(params object [] args){
            if(SafeHouse.Config.DebugEachOpcode && setlogmisc){
                logall(generalLogname, args);
            }

        }
        static public void clearOpcodeFile(){
            File.WriteAllText (opcodesfilename, "");
        }

    }


}
