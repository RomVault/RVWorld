using System;
using System.Collections.Generic;
using System.Text;

namespace TrrntZip
{
    public static class Workers
    {
        public static int ThreadId = 1;
        public static int requestedWorkers;
        public static int workers;
        public static object lockObj = new object();
    }
}
