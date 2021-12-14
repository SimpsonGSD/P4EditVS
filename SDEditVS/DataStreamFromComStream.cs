// Based on: https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/DataStreamFromComStream.cs
//
// MIT Licence: https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/LICENSE

/***************************************************************************
 
Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

namespace SDEditVS
{
    /// <summary>
    /// Implements a managed Stream object on top of a COM IStream.
    /// </summary>
    public sealed class DataStreamFromComStream : Stream, IDisposable
    {

        private IStream comStream;

        /// <summary>
        /// Build the managed Stream object on top of the IStream COM object
        /// </summary>
        /// <param name="comStream">The COM IStream object.</param>
        public DataStreamFromComStream(IStream comStream)
            : base()
        {
            this.comStream = comStream;
        }

        /// <summary>
        /// Gets or sets the position (relative to the stream's begin) inside the stream.
        /// </summary>
        public override long Position
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return Seek(0, SeekOrigin.Current);
            }

            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                Seek(value, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// True if it is possible to write on the stream.
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// True if it is possible to change the current position inside the stream.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// True if it is possible to read from the stream.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        public override long Length
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                long curPos = Position;
                long endPos = Seek(0, SeekOrigin.End);
                Position = curPos;
                return endPos - curPos;
            }
        }

        /// <summary>
        /// Throw a 'NotSupportedException'
        /// </summary>
        /// <param name="message">Unused parameter</param>
        private void _NotImpl(string message)
        {
            NotSupportedException ex = new NotSupportedException();
            throw ex;
        }

        /// <summary>
        /// Dispose this object and release the COM stream.
        /// </summary>
        public new void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (comStream != null)
                {
                    Flush();
                    comStream = null;
                }
            }
            finally
            {
                base.Dispose();
            }

        }

        /// <summary>
        /// Flush the pending data to the stream.
        /// </summary>
        public override void Flush()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (comStream != null)
            {
                try
                {
                    comStream.Commit(0);
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Reads a buffer of data from the stream.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="index">Starting position inside the buffer.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        public override int Read(byte[] buffer, int index, int count)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            uint bytesRead = 0;
            if (count > 0)
            {
                if (index == 0) comStream.Read(buffer, (uint)count, out bytesRead);
                else
                {
                    var tempBuffer = new byte[count];
                    comStream.Read(buffer, (uint)count, out bytesRead);
                    for (uint i = 0; i < bytesRead; ++i) buffer[index + i] = tempBuffer[i];
                }
            }

            return (int)bytesRead;
        }

        /// <summary>
        /// Sets the length of the stream.
        /// </summary>
        /// <param name="value">The new length.</param>
        public override void SetLength(long value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ULARGE_INTEGER ul = new ULARGE_INTEGER();
            ul.QuadPart = (ulong)value;
            comStream.SetSize(ul);
        }

        /// <summary>
        /// Changes the seek pointer to a new location relative to the current seek pointer
        /// or the beginning or end of the stream.
        /// </summary>
        /// <param name="offset">Displacement to be added to the location indicated by origin.</param>
        /// <param name="origin">Specifies the origin for the displacement.</param>
        /// <returns>Pointer to the location where this method writes the value of the new seek pointer from the beginning of the stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            LARGE_INTEGER l = new LARGE_INTEGER();
            ULARGE_INTEGER[] ul = new ULARGE_INTEGER[1];
            ul[0] = new ULARGE_INTEGER();
            l.QuadPart = offset;
            comStream.Seek(l, (uint)origin, ul);
            return (long)ul[0].QuadPart;
        }

        /// <summary>
        /// Writes a specified number of bytes into the stream object starting at the current seek pointer.
        /// </summary>
        /// <param name="buffer">The buffer to write into the stream.</param>
        /// <param name="index">Index inside the buffer of the first byte to write.</param>
        /// <param name="count">Number of bytes to write.</param>
        public override void Write(byte[] buffer, int index, int count)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (count > 0)
            {
                uint bytesWritten;
                if (index == 0) comStream.Write(buffer, (uint)count, out bytesWritten);
                else
                {
                    var tempBuffer = new byte[count];
                    for (Int32 i = 0; i < count; ++i) tempBuffer[i] = buffer[index + i];

                    comStream.Write(tempBuffer, (uint)count, out bytesWritten);
                }

                if (bytesWritten != count) throw new IOException("Didn't write enough bytes to IStream!");  // @TODO: Localize this.
            }
        }

        /// <summary>
        /// Close the stream.
        /// </summary>
        public override void Close()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (comStream != null)
            {
                Flush();
                comStream = null;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Empty destructor definition
        /// </summary>
        ~DataStreamFromComStream()
        {
            // CANNOT CLOSE NATIVE STREAMS IN FINALIZER THREAD
            // Close();
        }
    }
}
