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

using System;
using System.Collections.Generic;
using System.Linq;

namespace raztools
{
    public class CommandParser2
    {
        private Dictionary<string, Action<string[]>> m_commands = new Dictionary<string, Action<string[]>>();

        private static void EnsureArgCount(int got, int want)
        {
            if (got != want)
                throw new Exception(string.Format("got {0} args instead of {1}", got, want));
        }

        private static T Convert<T>(string value)
        {
            return (T)System.Convert.ChangeType(value, typeof(T));
        }

        #region Action generic overloads
        public void Add(string cmd, Action method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 0);
                method();
            });
        }

        public void Add<T>(string cmd, Action<T> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 1);
                method(Convert<T>(args[0]));
            });
        }

        public void Add<T1, T2>(string cmd, Action<T1, T2> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 2);
                method(Convert<T1>(args[0]), Convert<T2>(args[1]));
            });
        }

        public void Add<T1, T2, T3>(string cmd, Action<T1, T2, T3> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 3);
                method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]));
            });
        }

        public void Add<T1, T2, T3, T4>(string cmd, Action<T1, T2, T3, T4> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 4);
                method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]));
            });
        }

        public void Add<T1, T2, T3, T4, T5>(string cmd, Action<T1, T2, T3, T4, T5> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 5);
                method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]));
            });
        }

        public void Add<T1, T2, T3, T4, T5, T6>(string cmd, Action<T1, T2, T3, T4, T5, T6> method)
        {
            Add(cmd, args =>
            {
                EnsureArgCount(args.Length, 6);
                method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]), Convert<T6>(args[5]));
            });
        }
        #endregion

        private void Add(string cmd, Action<string[]> method)
        {
            m_commands.Add(cmd, method);
        }

        public string[] Commands
        {
            get { return m_commands.Keys.ToArray(); }
        }

        public void Exec(string cmdline)
        {
            if (string.IsNullOrEmpty(cmdline))
                return;

            var args = cmdline.Split(new char[] { ' ' });
            var cmd = args[0];
            args = args.Skip(1).ToArray();

            if (!m_commands.TryGetValue(cmd, out Action<string[]> method))
                throw new Exception("command not found: " + cmd);

            method(args);
        }
    }
}
