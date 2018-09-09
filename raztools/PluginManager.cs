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
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Remoting;

namespace raztools
{
    public class PluginManager<Plugin> : IDisposable where Plugin : class
    {
        static private SandboxDomain Domain { get; } = new SandboxDomain("plugins/");

        private ConcurrentDictionary<string, Plugin> m_plugins = new ConcurrentDictionary<string, Plugin>();

        static public string Available
        {
            get
            {
                return string.Join(", ", Domain.Classes.Select(c => c.TypeNme).ToArray());
            }
        }

        public string Current
        {
            get
            {
                return string.Join(", ", m_plugins.Keys);
            }
        }

        public Plugin Add(string plugin)
        {
            var plugin_obj = Domain.Create(plugin) as Plugin;
            if (plugin_obj != null && m_plugins.TryAdd(plugin, plugin_obj))
            {
                Domain.Unloaded += (sender, domain) => Remove(plugin);
                return plugin_obj;
            }

            return null;
        }

        public Plugin Get(string plugin)
        {
            Plugin plugin_obj = null;
            m_plugins.TryGetValue(plugin, out plugin_obj);
            return plugin_obj;
        }

        public bool Remove(string plugin)
        {
            Plugin plugin_obj;
            if (m_plugins.TryRemove(plugin, out plugin_obj))
            {
                (plugin_obj as IDisposable)?.Dispose();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            foreach (var plugin in m_plugins.Values)
            {
                try
                {
                    (plugin as IDisposable)?.Dispose();
                }
                catch (RemotingException)
                {
                }
            }
            m_plugins.Clear();
        }

        static public void Load()
        {
            Domain.Load();
        }

        static public void Unload()
        {
            Domain.Unload();
        }
    }
}
