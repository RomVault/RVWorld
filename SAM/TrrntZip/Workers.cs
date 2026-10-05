using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace TrrntZip
{
    public static class Workers
    {
        public static int ThreadId = 1;
        public static int requestedWorkers;
        public static int workers;
        public static object lockObj = new object();


        public static void RemoveAllWorks()
        {
            lock (lockObj)
            {
                requestedWorkers = 0;
                if (Workers.workers > 0)
                {
                    int extraWorkers = Workers.workers;
                    for (int i = 0; i < extraWorkers; i++)
                        MainQueue.bccFile.Add(new cFile() { fileId = -1, filename = "Removing" });
                }
            }

        }
    }
}
