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
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace raztools
{
    public class CommandParser : IDisposable
    {
        public class Command
        {
            public string Template { get; private set; }
            public Regex RegexPattern { get; private set; }
            public Delegate Method { get; private set; }

            public Command(string template, Delegate method)
            {
                string pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{[0-9]+\}", "(.*?)") + "$";

                Template = template;
                RegexPattern = new Regex(pattern);
                Method = method;
            }

            public string[] MatchInput(string input)
            {
                Match match = RegexPattern.Match(input);

                if (!match.Success)
                    return null;

                return match.Groups.Cast<Group>().Select(g => g.Value).Skip(1).ToArray();
            }

            public bool Invoke(string input)
            {
                var args = MatchInput(input);
                if (args != null)
                {
                    Invoke(Method, args);
                    return true;
                }

                return false;
            }

            static private void Invoke(Delegate method, string[] args)
            {
                var parameters = method.Method.GetParameters();
                var converted_args = new List<object>();

                for (int i = 0; i < parameters.Length; ++i)
                {
                    var argtype = parameters[i].ParameterType;
                    var arg = TypeDescriptor.GetConverter(argtype).ConvertFromString(args[i]);
                    converted_args.Add(arg);
                }

                method.DynamicInvoke(converted_args.ToArray());
            }
        }

        private List<Command> m_commands = new List<Command>();

        public event EventHandler<Exception> Exceptions;

        #region Action generic overloads
        public void Add(string template, Action method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T>(string template, Action<T> method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T1, T2>(string template, Action<T1, T2> method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T1, T2, T3>(string template, Action<T1, T2, T3> method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T1, T2, T3, T4>(string template, Action<T1, T2, T3, T4> method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T1, T2, T3, T4, T5>(string template, Action<T1, T2, T3, T4, T5> method)
        {
            Add(template, (Delegate)method);
        }

        public void Add<T1, T2, T3, T4, T5, T6>(string template, Action<T1, T2, T3, T4, T5, T6> method)
        {
            Add(template, (Delegate)method);
        }
        #endregion

        public void Add(string template, Delegate method)
        {
            m_commands.Add(new Command(template, method));
        }

        public string[] Commands
        {
            get { return m_commands.Select(cmd => cmd.Template).ToArray(); }
        }

        public void Exec(string cmdline)
        {
            foreach (var cmd in m_commands)
            {
                try
                {
                    if (cmd.Invoke(cmdline))
                        return;
                }
                catch (Exception e)
                {
                    // get inner exception
                    for (; e.InnerException != null; e = e.InnerException) ;

                    Exceptions?.Invoke(this, e);
                }
            }
        }

        public void Dispose()
        {
            m_commands.Clear();
            Exceptions = null;
        }
    }
}
