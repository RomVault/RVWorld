/*
  Copyright (c) 1990-2008 Info-ZIP.  All rights reserved.

  See the accompanying file LICENSE, version 2000-Apr-09 or later
  (the contents of which are also included in unzip.h) for terms of use.
  If, for some reason, all these files are missing, the Info-ZIP license
  also may be found at:  ftp://ftp.info-zip.org/pub/infozip/license.html
*/
/*---------------------------------------------------------------------------

  unshrink.c                     version 1.22                     19 Mar 2008


       NOTE:  This code may or may not infringe on the so-called "Welch
       patent" owned by Unisys.  (From reading the patent, it appears
       that a pure LZW decompressor is *not* covered, but this claim has
       not been tested in court, and Unisys is reported to believe other-
       wise.)  It is therefore the responsibility of the user to acquire
       whatever license(s) may be required for legal use of this code.

       THE INFO-ZIP GROUP DISCLAIMS ALL LIABILITY FOR USE OF THIS CODE
       IN VIOLATION OF APPLICABLE PATENT LAW.


  Shrinking is basically a dynamic LZW algorithm with allowed code sizes of
  up to 13 bits; in addition, there is provision for partial clearing of
  leaf nodes.  PKWARE uses the special code 256 (decimal) to indicate a
  change in code size or a partial clear of the code tree:  256,1 for the
  former and 256,2 for the latter.  [Note that partial clearing can "orphan"
  nodes:  the parent-to-be can be cleared before its new child is added,
  but the child is added anyway (as an orphan, as though the parent still
  existed).  When the tree fills up to the point where the parent node is
  reused, the orphan is effectively "adopted."  Versions prior to 1.05 were
  affected more due to greater use of pointers (to children and siblings
  as well as parents).]

  This replacement version of unshrink.c was written from scratch.  It is
  based only on the algorithms described in Mark Nelson's _The Data Compres-
  sion Book_ and in Terry Welch's original paper in the June 1984 issue of
  IEEE _Computer_; no existing source code, including any in Nelson's book,
  was used.

  Memory requirements have been reduced in this version and are now no more
  than the original Sam Smith code.  This is still larger than any of the
  other algorithms:  at a minimum, 8K+8K+16K (stack+values+parents) assuming
  16-bit short ints, and this does not even include the output buffer (the
  other algorithms leave the uncompressed data in the work area, typically
  called slide[]).  For machines with a 64KB data space this is a problem,
  particularly when text conversion is required and line endings have more
  than one character.  UnZip's solution is to use two roughly equal halves
  of outbuf for the ASCII conversion in such a case; the "unshrink" argument
  to flush() signals that this is the case.

  For large-memory machines, a second outbuf is allocated for translations,
  but only if unshrinking and only if translations are required.

              | binary mode  |        text mode
    ---------------------------------------------------
    big mem   |  big outbuf  | big outbuf + big outbuf2  <- malloc'd here
    small mem | small outbuf | half + half small outbuf

  Copyright 1994, 1995 Greg Roelofs.  See the accompanying file "COPYING"
  in UnZip 5.20 (or later) source or binary distributions.

  ---------------------------------------------------------------------------*/

using System;
using System.IO;

namespace Compress.Support.Compression.Shrink
{
    public class UnShrink:Stream
    {
        private ulong compressedSize;
        private long unCompressedSize;

        private byte[] byteOut;
        private long outBytesCount;

        private ulong inByteCount;
        private Stream inStream;

        static uint[] mask_bits = new uint[]
        {
            0x0000,
            0x0001, 0x0003, 0x0007, 0x000f, 0x001f, 0x003f, 0x007f, 0x00ff,
            0x01ff, 0x03ff, 0x07ff, 0x0fff, 0x1fff, 0x3fff, 0x7fff, 0xffff
        };

        private const int PK_OK = 0;
        private const int PK_ERR = 2;
        private const int EOF = 1234;

        private const int MAX_BITS = 13;

        private const int WSIZE = 65536;            /* window size--must be a power of two */
        private const int HSIZE = 0x2000;           /* HSIZE is defined as 2^13 (8192) in unzip.h (resp. unzpriv.h */
        private const int BOGUSCODE = 256;
        private const int CODE_MASK = (HSIZE - 1);  /* 0x1fff (lower bits are parent's index) */
        private const int FREE_CODE = HSIZE;        /* 0x2000 (code is unused or was cleared) */
        private const int HAS_CHILD = (HSIZE << 1); /* 0x4000 (code has a child--do not clear) */
        private const int lenEOL = 2;
        private const int OUTBUFSIZ = (lenEOL * WSIZE); /* more efficient text conversion */
        private const int RAWBUFSIZ = OUTBUFSIZ;

        //#  define OUTDBG(c)

        private bool G_textmode = false;
        private byte[] G_outbuf;
        private int[] G_value;
        private int[] G_Parent;
        private int[] G_Stack;

        private long G_outcnt;
        
        private int G_bits_left;
        private bool G_zipeof;
        private ulong G_bitbuf;

        public UnShrink(Stream inStr, ulong compsize,ulong unCompSize,out ZipReturn result)
        {
            inStream = inStr;
            compressedSize = compsize;
            unCompressedSize = (long)unCompSize;
            inByteCount = 0;

            byteOut=new byte[unCompressedSize];
            outBytesCount = 0;
            try
            {
                unshrink();
            }
            catch(Exception e) {
                result = ZipReturn.ZipDecodeError;
                return;
            }
            if (outBytesCount != unCompressedSize)
            {
                result = ZipReturn.ZipDecodeError;
                return;
            }
            outBytesCount = 0;
            result = ZipReturn.ZipGood;
        }
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                buffer[offset + i] = byteOut[outBytesCount++];
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => unCompressedSize;
        public override long Position
        {
            get => outBytesCount;
            set { }
        }


        private int flush(byte[] p, long d)
        {
            for (int i = 0; i < d; i++)
                byteOut[outBytesCount++] = p[i];
            return 0;
        }

        private int NEXTBYTE()
        {
            if (inByteCount == compressedSize)
                return EOF;
            inByteCount++;
            return inStream.ReadByte();
        }

        private void READBITS(int nbits, out int zdest)
        {
            if (nbits > G_bits_left)
            {
                int temp;
                G_zipeof = true;
                while (G_bits_left <= 8 * (int)(4 - 1) && (temp = NEXTBYTE()) != EOF)
                {
                    G_bitbuf |= (ulong)temp << G_bits_left;
                    G_bits_left += 8;
                    G_zipeof = false;
                }
            }
            zdest = (int)(G_bitbuf & (ulong)mask_bits[nbits]);
            G_bitbuf >>= nbits;
            G_bits_left -= nbits;
        }


        /***********************/
        /* Function unshrink() */
        /***********************/

        public int unshrink()
        {
            int idxStackTop = HSIZE - 1;
            int finalval;
            int codesize = 9;
            int len;
            int error;
            int code;
            int oldcode;
            int lastfreecode;
            int outbufsiz;

            /*---------------------------------------------------------------------------
                Initialize various variables.
              ---------------------------------------------------------------------------*/

            lastfreecode = BOGUSCODE;

            /* non-memory-limited machines:  allocate second (large) buffer for
             * textmode conversion in flush(), but only if needed */
            ///TODO if (G_textmode && !G_outbuf2 && (G_outbuf2 = (uch*) malloc(TRANSBUFSIZ)) == (uch*) NULL)
            ///TODO     return PK_MEM3;

            G_value = new int[HSIZE];
            G_Parent = new int[HSIZE];


            for (code = 0; code < BOGUSCODE; ++code)
            {
                G_value[code] = code;
                G_Parent[code] = BOGUSCODE;
            }

            for (code = BOGUSCODE + 1; code < HSIZE; ++code)
                G_Parent[code] = FREE_CODE;

            if (G_textmode)
                outbufsiz = RAWBUFSIZ;
            else
                outbufsiz = OUTBUFSIZ;
            int outIdx = 0;

            G_outbuf = new byte[outbufsiz];
            G_Stack = new int[outbufsiz];
            G_outcnt = 0L;

            /*---------------------------------------------------------------------------
                Get and output first code, then loop over remaining ones.
              ---------------------------------------------------------------------------*/

            READBITS(codesize, out oldcode);
            if (G_zipeof)
                return PK_OK;

            finalval = oldcode;
            //OUTDBG(finalval)
            G_outbuf[outIdx++] = (byte)finalval;
            G_outcnt++;

            while (true)
            {
                READBITS(codesize, out code);
                if (G_zipeof)
                    break;
                if (code == BOGUSCODE)
                {
                    /* possible to have consecutive escapes? */
                    READBITS(codesize, out code);
                    if (G_zipeof)
                        break;
                    if (code == 1)
                    {
                        ++codesize;
                        if (codesize > MAX_BITS) return PK_ERR;
                    }
                    else if (code == 2)
                    {
                        /* clear leafs (nodes with no children) */
                        partial_clear(lastfreecode);
                        lastfreecode = BOGUSCODE; /* reset start of free-node search */
                    }

                    continue;
                }

                /*-----------------------------------------------------------------------
                    Translate code:  traverse tree from leaf back to root.
                  -----------------------------------------------------------------------*/

                int idxNewStr = idxStackTop;
                int curcode = code;

                if (G_Parent[code] == FREE_CODE)
                {
                    /* or (FLAG_BITS[code] & FREE_CODE)? */
                    G_Stack[idxNewStr--] = finalval;
                    code = oldcode;
                }

                while (code != BOGUSCODE)
                {
                    if (idxNewStr < 0)
                    {
                        /* Bogus compression stream caused buffer underflow! */
                        return PK_ERR;
                    }

                    if (G_Parent[code] == FREE_CODE)
                    {
                        /* or (FLAG_BITS[code] & FREE_CODE)? */
                        G_Stack[idxNewStr--] = finalval;
                        code = oldcode;
                    }
                    else
                    {
                        G_Stack[idxNewStr--] = G_value[code];
                        code = (G_Parent[code] & CODE_MASK);
                    }
                }

                len = (idxStackTop - idxNewStr++);
                finalval = G_Stack[idxNewStr];

                /*-----------------------------------------------------------------------
                    Write expanded string in reverse order to output buffer.
                  -----------------------------------------------------------------------*/

                /*
                Console.WriteLine(
                    "code %4d; oldcode %4d; char %3d (%c); len %d; string [", curcode,
                    oldcode, (int) (*newstr), (*newstr < 32 || *newstr >= 127) ? ' ' : *newstr,
                    len);
                */
                {
                    for (int p = idxNewStr; p < idxNewStr + len; ++p)
                    {
                        G_outbuf[outIdx++] = (byte)G_Stack[p];
                        //OUTDBG(*p)
                        if (++G_outcnt == outbufsiz)
                        {
                            if ((error = flush(G_outbuf, G_outcnt)) != 0)
                            {
                                return error;
                            }

                            outIdx = 0;
                            G_outcnt = 0L;
                        }
                    }
                }

                /*-----------------------------------------------------------------------
                    Add new leaf (first character of newstr) to tree as child of oldcode.
                  -----------------------------------------------------------------------*/

                /* search for freecode */
                code = (lastfreecode + 1);
                /* add if-test before loop for speed? */
                while ((code < HSIZE) && (G_Parent[code] != FREE_CODE))
                    ++code;
                lastfreecode = code;
                if (code >= HSIZE)
                    /* invalid compressed data caused max-code overflow! */
                    return PK_ERR;

                G_value[code] = finalval;
                G_Parent[code] = oldcode;
                oldcode = curcode;

            }

            /*---------------------------------------------------------------------------
                Flush any remaining data and return to sender...
              ---------------------------------------------------------------------------*/

            if (G_outcnt > 0L)
            {
                if ((error = flush(G_outbuf, G_outcnt)) != 0)
                {
                    return error;
                }
            }

            return PK_OK;

        } /* end function unshrink() */





        /****************************/
        /* Function partial_clear() */ /* no longer recursive... */
        /****************************/

        private void partial_clear(int lastcodeused)
        {
            int code;

            /* clear all nodes which have no children (i.e., leaf nodes only) */

            /* first loop:  mark each parent as such */
            for (code = BOGUSCODE + 1; code <= lastcodeused; ++code)
            {
                int cparent = (int)(G_Parent[code] & CODE_MASK);

                if (cparent > BOGUSCODE)
                    G_Parent[cparent] |= HAS_CHILD; /* set parent's child-bit */
            }

            /* second loop:  clear all nodes *not* marked as parents; reset flag bits */
            for (code = BOGUSCODE + 1; code <= lastcodeused; ++code)
            {
                if ((G_Parent[code] & HAS_CHILD) != 0) /* just clear child-bit */
                    G_Parent[code] &= ~HAS_CHILD;
                else
                {
                    /* leaf:  lose it */
                    G_Parent[code] = FREE_CODE;
                }
            }
        }

      
    }
}