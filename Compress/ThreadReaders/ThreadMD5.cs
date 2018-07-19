using System;
using System.Security.Cryptography;
using System.Threading;

namespace Compress.ThreadReaders
{
    public class ThreadMD5 : IDisposable
    {
        private readonly AutoResetEvent _waitEvent;
        private readonly AutoResetEvent _outEvent;
        private readonly Thread _tWorker;

        private readonly MD5 _md5;

        private byte[] _buffer;
        private int _size;
        private bool _finished;

        public ThreadMD5()
        {
            _waitEvent = new AutoResetEvent(false);
            _outEvent = new AutoResetEvent(false);
            _finished = false;
            _md5 = MD5.Create();

            _tWorker = new Thread(MainLoop);
            _tWorker.Start();
        }

        public byte[] Hash => _md5.Hash;

        public void Dispose()
        {
            _waitEvent.Close();
            _outEvent.Close();
            // _md5.Dispose();
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
                _md5.TransformBlock(_buffer, 0, _size, null, 0);
                _outEvent.Set();
            }

            byte[] tmp = new byte[0];
            _md5.TransformFinalBlock(tmp, 0, 0);
        }

        public void Trigger(byte[] buffer, int size)
        {
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