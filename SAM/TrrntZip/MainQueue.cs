using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TrrntZip
{
    public static class MainQueue
    {
        public static BlockingCollection<cFile> bccFile = new BlockingCollection<cFile>();

        public static PauseCancel pc = new PauseCancel();

        public static int fileCount;

        /// <summary>
        /// Number of worker threads that are actually processing a file, i.e. that
        /// are not just sitting blocked inside bccFile.Take().
        /// </summary>
        private static int _busyWorkers;

        /// <summary>
        /// Number of producers (FileAdder) that are still scanning for and adding files.
        /// </summary>
        private static int _activeProducers;

        private static readonly object _stateLock = new object();

        /// <summary>
        /// Raised whenever the overall busy state changes. True when any worker is
        /// busy, any item is queued, or a FileAdder is still adding files.
        /// Raised on a worker/producer thread, so marshal to the UI thread if needed.
        /// </summary>
        public static event Action<bool> BusyChanged;

        private static bool _isBusy;

        /// <summary>
        /// True if any worker is processing a file, or there are entries waiting on
        /// the queue, or a FileAdder is still adding files.
        /// </summary>
        public static bool IsBusy
        {
            get
            {
                lock (_stateLock)
                {
                    return CalculateBusy();
                }
            }
        }

        public static int BusyWorkers => Volatile.Read(ref _busyWorkers);

        private static bool CalculateBusy()
        {
            return _activeProducers != 0 || _busyWorkers != 0 || bccFile.Count != 0;
        }

        /// <summary>Called by a worker immediately after it takes an item off the queue.</summary>
        public static void WorkerBusy()
        {
            lock (_stateLock)
            {
                _busyWorkers++;
                RaiseIfChanged();
            }
        }

        /// <summary>Called by a worker when it has finished an item and is about to block on Take() again.</summary>
        public static void WorkerIdle()
        {
            lock (_stateLock)
            {
                _busyWorkers--;
                RaiseIfChanged();
            }
        }

        /// <summary>Called by a producer before it starts adding files to the queue.</summary>
        public static void BeginProducing()
        {
            lock (_stateLock)
            {
                _activeProducers++;
                RaiseIfChanged();
            }
        }

        /// <summary>Called by a producer once it has added all of its files.</summary>
        public static void EndProducing()
        {
            lock (_stateLock)
            {
                _activeProducers--;
                RaiseIfChanged();
            }
        }

        /// <summary>
        /// Called after items are added to or drained from the queue outside of the
        /// normal producer/worker flow, so the busy state can be re-evaluated.
        /// </summary>
        public static void QueueChanged()
        {
            lock (_stateLock)
            {
                RaiseIfChanged();
            }
        }

        private static void RaiseIfChanged()
        {
            bool busy = CalculateBusy();

            if (busy == _isBusy)
                return;

            _isBusy = busy;
            BusyChanged?.Invoke(busy);
        }
    }
}
