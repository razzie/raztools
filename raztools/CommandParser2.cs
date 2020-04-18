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
        private Dictionary<string, Command> m_commands = new Dictionary<string, Command>();

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
        public void Add(string cmd, Action method) => Add(cmd, Command.FromAction(method));
        public void Add<T>(string cmd, Action<T> method) => Add(cmd, Command.FromAction(method));
        public void Add<T1, T2>(string cmd, Action<T1, T2> method) => Add(cmd, Command.FromAction(method));
        public void Add<T1, T2, T3>(string cmd, Action<T1, T2, T3> method) => Add(cmd, Command.FromAction(method));
        public void Add<T1, T2, T3, T4>(string cmd, Action<T1, T2, T3, T4> method) => Add(cmd, Command.FromAction(method));
        public void Add<T1, T2, T3, T4, T5>(string cmd, Action<T1, T2, T3, T4, T5> method) => Add(cmd, Command.FromAction(method));
        public void Add<T1, T2, T3, T4, T5, T6>(string cmd, Action<T1, T2, T3, T4, T5, T6> method) => Add(cmd, Command.FromAction(method));
        #endregion // Action generic overloads

        #region Func generic overloads with return value
        public void AddWithReturnValue<R>(string cmd, Func<R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T, R>(string cmd, Func<T, R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T1, T2, R>(string cmd, Func<T1, T2, R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T1, T2, T3, R>(string cmd, Func<T1, T2, T3, R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T1, T2, T3, T4, R>(string cmd, Func<T1, T2, T3, T4, R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T1, T2, T3, T4, T5, R>(string cmd, Func<T1, T2, T3, T4, T5, R> method) => Add(cmd, Command.FromTask(method));
        public void AddWithReturnValue<T1, T2, T3, T4, T5, T6, R>(string cmd, Func<T1, T2, T3, T4, T5, T6, R> method) => Add(cmd, Command.FromTask(method));
        #endregion // Func generic overloads with return value

        public void Add(string cmd, Command method)
        {
            m_commands.Add(cmd, method);
        }

        public string[] Commands
        {
            get { return m_commands.Keys.ToArray(); }
        }

        public object Exec(string cmdline)
        {
            if (string.IsNullOrEmpty(cmdline))
                return null;

            var args = cmdline.Split(new char[] { ' ' });
            var cmd = args[0];
            args = args.Skip(1).ToArray();

            if (!m_commands.TryGetValue(cmd, out Command method))
                throw new Exception("command not found: " + cmd);

            return method.Invoke(args);
        }

        public class Command
        {
            private Func<string[], object> m_func;

            private Command(Func<string[], object> func)
            {
                m_func = func;
            }
            
            public object Invoke(string[] args)
            {
                return m_func(args);
            }

            public static Command FromAction(Action method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 0);
                    method();
                    return null;
                });
            }

            public static Command FromAction<T>(Action<T> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 1);
                    method(Convert<T>(args[0]));
                    return null;
                });
            }

            public static Command FromAction<T1, T2>(Action<T1, T2> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 2);
                    method(Convert<T1>(args[0]), Convert<T2>(args[1]));
                    return null;
                });
            }

            public static Command FromAction<T1, T2, T3>(Action<T1, T2, T3> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 3);
                    method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]));
                    return null;
                });
            }

            public static Command FromAction<T1, T2, T3, T4>(Action<T1, T2, T3, T4> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 4);
                    method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]));
                    return null;
                });
            }

            public static Command FromAction<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 5);
                    method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]));
                    return null;
                });
            }

            public static Command FromAction<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 6);
                    method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]), Convert<T6>(args[5]));
                    return null;
                });
            }

            public static Command FromTask<R>(Func<R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 0);
                    return method();
                });
            }

            public static Command FromTask<T, R>(Func<T, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 1);
                    return method(Convert<T>(args[0]));
                });
            }

            public static Command FromTask<T1, T2, R>(Func<T1, T2, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 2);
                    return method(Convert<T1>(args[0]), Convert<T2>(args[1]));
                });
            }

            public static Command FromTask<T1, T2, T3, R>(Func<T1, T2, T3, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 3);
                    return method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]));
                });
            }

            public static Command FromTask<T1, T2, T3, T4, R>(Func<T1, T2, T3, T4, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 4);
                    return method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]));
                });
            }

            public static Command FromTask<T1, T2, T3, T4, T5, R>(Func<T1, T2, T3, T4, T5, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 5);
                    return method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]));
                });
            }

            public static Command FromTask<T1, T2, T3, T4, T5, T6, R>(Func<T1, T2, T3, T4, T5, T6, R> method)
            {
                return new Command(args =>
                {
                    EnsureArgCount(args.Length, 6);
                    return method(Convert<T1>(args[0]), Convert<T2>(args[1]), Convert<T3>(args[2]), Convert<T4>(args[3]), Convert<T5>(args[4]), Convert<T6>(args[5]));
                });
            }
        }
    }
}
