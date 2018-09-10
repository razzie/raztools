/*
Copyright (C) Gábor "Razzie" Görzsöny
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
*/

using raztools;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace examples
{
#pragma warning disable CS0649
    class DummyClass1
    {
        public int member1;
        public float member2;
        public DummyClass2 member3;
    }

    class DummyClass2
    {
        public int member1;
        public int[] member2;
    }
#pragma warning restore CS0649

    public class ObjectDeserializerExample : IExample
    {
        public int Run()
        {
            var cache = new Dictionary<string, object>();
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream("examples.Resources.dummy.txt"))
            {
                ObjectDeserializer.ParseStream(stream, cache);
            }

            foreach (var item in cache)
            {
                Console.WriteLine("{0} - {1}", item.Key.ToString(), item.Value.ToString());
            }

            return 0;
        }
    }
}
