using System;
using System.Threading;
using Compress.Support.Utils;

namespace Compress.ThreadReaders
{
    public class ThreadCRC : IDisposable
    {
        private CRC crc; 
        private readonly AutoResetEvent _waitEvent;
        private readonly AutoResetEvent _outEvent;
        private readonly Thread _tWorker;

        private byte[] _buffer;
        private int _size;
        private bool _finished;


        public ThreadCRC()
        {
            crc=new CRC();
            _waitEvent = new AutoResetEvent(false);
            _outEvent = new AutoResetEvent(false);
            _finished = false;

            _tWorker = new Thread(MainLoop);
            _tWorker.Start();
        }

        public byte[] Hash => crc.Crc32ResultB;

        public void Dispose()
        {
            _waitEvent.Dispose();
            _outEvent.Dispose();
        }

        private void MainLoop()
        {
            while (true)
            {
                _waitEvent.WaitOne();
                if (_finished)
                {
                    break;
                }

                crc.SlurpBlock(_buffer,0,_size);

                _outEvent.Set();
            }
        }

        public void Trigger(byte[] buffer, int size)
        {
            _buffer = buffer;
            _size = size;
            _waitEvent.Set();
        }
        public void TriggerOnce(byte[] buffer, int size)
        {
            crc.Reset();
            _buffer = buffer;
            _size = size;
            _waitEvent.Set();
        }

        public void Wait()
        {
            _outEvent.WaitOne();
        }

        public void Finish()
        {
            _finished = true;
            _waitEvent.Set();
            _tWorker.Join();
        }
    }
}