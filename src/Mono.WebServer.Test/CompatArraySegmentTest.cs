//
// CompatArraySegmentTest.cs
//
// Author:
//   Leonardo Taglialegne <leonardo.taglialegne@gmail.com>
//
// Copyright (c) 2013 Leonardo Taglialegne.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Mono.WebServer.FastCgi.Compatibility;
using NUnit.Framework;
using System;

namespace Mono.WebServer.Test {
	
	// [TestFixture]
	public class CompatArraySegmentTest
	{
		[Test]
		public void TestCase ()
		{
			var test = new CompatArraySegment<int> (new int[1]);

			test [0] = -3;
			Assert.AreEqual (-3, test [0]);

			try {
				var test2 = new CompatArraySegment<int> (new int[1]);
				test2 [1] = 0;
				Assert.Fail ("Out of range access");
			} catch (ArgumentOutOfRangeException) {
			}
		}
	}
}

