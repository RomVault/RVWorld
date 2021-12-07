using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace CHDlib
{
    class CHDThreadReadBuffer
    {
        private readonly AutoResetEvent _waitEvent;
        private readonly AutoResetEvent _outEvent;
        private readonly Thread _tWorker;

        private byte[] _buffer;
        private byte[] _crc;
        private readonly hard_disk_info _hd;
        private bool _finished;

        public bool errorState;

        public hdErr SizeRead;
        private int _block;

        public CHDThreadReadBuffer(hard_disk_info hardDisk)
        {
            _waitEvent = new AutoResetEvent(false);
            _outEvent = new AutoResetEvent(false);
            _finished = false;
            _hd = hardDisk;
            errorState = false;

            _tWorker = new Thread(MainLoop);
            _tWorker.Start();
        }

        public void Dispose()
        {
            _waitEvent.Close();
            _outEvent.Close();
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
                try
                {
                    SizeRead = read_block_into_cache(_block, _buffer,ref _crc);
                }
                catch (Exception)
                {
                    errorState = true;
                }
                _outEvent.Set();
            }
        }

        public void Trigger(int block, byte[] buffer, ref byte[] crc)
        {
            _block = block;
            _buffer = buffer;
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


        private const int HDFLAGS_HAS_PARENT = 0x00000001;
        private const int HDFLAGS_IS_WRITEABLE = 0x00000002;
        private const int HDCOMPRESSION_ZLIB = 1;
        private const int HDCOMPRESSION_ZLIB_PLUS = 2;
        private const int HDCOMPRESSION_MAX = 3;

        public hdErr read_block_into_cache(int block, byte[] cache, ref byte[] crc)
        {
            crc = null;
            mapentry mapEntry = _hd.map[block];
            switch (mapEntry.flags & mapFlags.MAP_ENTRY_FLAG_TYPE_MASK)
            {
                case mapFlags.MAP_ENTRY_TYPE_COMPRESSED:
                    {

                        if (mapEntry.BlockCache != null)
                        {
                            Buffer.BlockCopy(mapEntry.BlockCache, 0, cache, 0, (int)_hd.blocksize);
                            //already checked CRC for this block when the cache was made
                            break;
                        }

                        _hd.file.Seek((long)_hd.map[block].offset, SeekOrigin.Begin);

                        switch (_hd.compression)
                        {
                            case HDCOMPRESSION_ZLIB:
                            case HDCOMPRESSION_ZLIB_PLUS:
                                {
                                    using (var st = new System.IO.Compression.DeflateStream(_hd.file, System.IO.Compression.CompressionMode.Decompress, true))
                                    {
                                        int bytes = st.Read(cache, 0, (int)_hd.blocksize);
                                        if (bytes != (int)_hd.blocksize)
                                            return hdErr.HDERR_READ_ERROR;

                                        if (mapEntry.UseCount > 0)
                                        {
                                            mapEntry.BlockCache = new byte[bytes];
                                            Buffer.BlockCopy(cache, 0, mapEntry.BlockCache, 0, bytes);
                                        }

                                    }

                                    if ((mapEntry.flags & mapFlags.MAP_ENTRY_FLAG_NO_CRC) == 0)
                                        _crc = BitConverter.GetBytes(mapEntry.crc);
                                    break;
                                }
                            default:
                                {
                                    Console.WriteLine("Unknown compression");
                                    return hdErr.HDERR_DECOMPRESSION_ERROR;

                                }
                        }
                        break;
                    }

                case mapFlags.MAP_ENTRY_TYPE_UNCOMPRESSED:
                    {
                        _hd.file.Seek((long)_hd.map[block].offset, SeekOrigin.Begin);
                        int bytes = _hd.file.Read(cache, 0, (int)_hd.blocksize);

                        if (bytes != (int)_hd.blocksize)
                            return hdErr.HDERR_READ_ERROR;
                        if ((mapEntry.flags & mapFlags.MAP_ENTRY_FLAG_NO_CRC) == 0)
                            _crc = BitConverter.GetBytes(mapEntry.crc);
                        break;
                    }

                case mapFlags.MAP_ENTRY_TYPE_MINI:
                    {
                        byte[] tmp = BitConverter.GetBytes(_hd.map[block].offset);
                        for (int i = 0; i < 8; i++)
                        {
                            cache[i] = tmp[7 - i];
                        }

                        for (int i = 8; i < _hd.blocksize; i++)
                        {
                            cache[i] = cache[i - 8];
                        }
                        if ((mapEntry.flags & mapFlags.MAP_ENTRY_FLAG_NO_CRC) == 0)
                            _crc = BitConverter.GetBytes(mapEntry.crc);
                        break;
                    }

                case mapFlags.MAP_ENTRY_TYPE_SELF_HUNK:
                    {
                        hdErr ret = read_block_into_cache((int)mapEntry.offset, cache,ref crc);
                        if (ret != hdErr.HDERR_NONE)
                            return ret;
                        break;
                    }
                default:
                    return hdErr.HDERR_DECOMPRESSION_ERROR;

            }
            return hdErr.HDERR_NONE;
        }
    }
}
