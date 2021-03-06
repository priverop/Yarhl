﻿// NodeContainerFormat.cs
//
// Author:
//       Benito Palacios Sánchez <benito356@gmail.com>
//
// Copyright (c) 2016 Benito Palacios Sánchez
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
namespace Yarhl.FileSystem
{
    using System;
    using Yarhl.FileFormat;

    /// <summary>
    /// Node container format for unpack / pack files.
    /// </summary>
    public class NodeContainerFormat : IFormat, IDisposable
    {
        bool manageRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeContainerFormat"/>
        /// class.
        /// </summary>
        public NodeContainerFormat()
        {
            Root = new Node("NodeContainerRoot");
            manageRoot = true;
        }

        /// <summary>
        /// Gets the root node containing the children.
        /// </summary>
        public Node Root {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="NodeContainerFormat"/>
        /// is disposed.
        /// </summary>
        public bool Disposed {
            get;
            private set;
        }

        /// <summary>
        /// Moves the children from this format to a <see cref="Node"/>.
        /// </summary>
        /// <remarks>
        /// <para>The node will handle the lifecycle of the children.
        /// Disposing the format won't dispose the children.</para>
        /// </remarks>
        /// <param name="newNode">Node that will contain the children.</param>
        public void MoveChildrenTo(Node newNode)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(NodeContainerFormat));

            if (newNode == null)
                throw new ArgumentNullException(nameof(newNode));

            newNode.Add(Root.Children);
            Root = newNode;
            manageRoot = false;
        }

        /// <summary>
        /// Releases all resource used by the <see cref="NodeContainerFormat"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resource used by the
        /// <see cref="NodeContainerFormat"/> object.
        /// </summary>
        /// <param name="disposing">
        /// If set to <see langword="true" /> free managed resources also.
        /// It happens from Dispose() calls.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            Disposed = true;
            if (disposing && manageRoot) {
                Root.Dispose();
            }
        }
    }
}
