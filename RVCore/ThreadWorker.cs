using System.Threading;

namespace RVCore
{

    public delegate void WorkerStart(ThreadWorker work);
    public delegate void WorkerReport(object obj);
    public delegate void Worker();

    public class ThreadWorker
    {
        private readonly WorkerStart _startFunc;

        public Worker wStarting;
        public Worker wFinal;
        public Worker wCancel;

        public WorkerReport wReport;

        public bool CancellationPending;

        public ThreadWorker(WorkerStart startFunc)
        {
            _startFunc = startFunc;
        }

        public void Cancel()
        {
            CancellationPending = true;
            wCancel?.Invoke();
        }

        public void StartAsync()
        {
            CancellationPending = false;
            Thread t1 = new Thread(() =>
            {
                wStarting?.Invoke();
                _startFunc(this);
                wFinal?.Invoke();
            });
            t1.Start();
        }

        public void Start()
        {
            CancellationPending = false;
            wStarting?.Invoke();
            _startFunc(this);
            wFinal?.Invoke();
        }

        public void Report(object obj)
        {
            wReport?.Invoke(obj);
        }

    }

}
