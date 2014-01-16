using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramDeployerClient
{
    public class BinaryBuffer
    {
        private byte[] buffer, initial;

        public Stream BaseStream { get; private set; }

        public BinaryBuffer(int size, byte[] initialBuffer, Stream stream)
        {
            initial = initialBuffer;
            buffer = initialBuffer ?? new byte[size];
            BaseStream = stream;
        }

        public byte[] Peek()
        {
            return buffer;
        }

        public int Read(ref byte[] buf)
        {
            if (initial != null)
            {
                buf = new byte[initial.Length];
                Array.Copy(initial, buf, buf.Length);
                initial = null;
                return buf.Length;
            }
            return BaseStream.Read(buf, 0, buf.Length);
        }
    }
}
