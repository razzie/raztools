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
using System.Runtime.Remoting;

namespace raztools
{
    public class PluginManager<Plugin> : IDisposable where Plugin : class
    {
        static private ConcurrentDictionary<string, PluginDomain> Domains { get; } = new ConcurrentDictionary<string, PluginDomain>();

        protected PluginDomain Domain { get; private set; }
        protected ConcurrentDictionary<string, Plugin> Plugins { get; } = new ConcurrentDictionary<string, Plugin>();

        public PluginManager(string plugin_folder)
        {
            if (Domains.TryGetValue(plugin_folder, out PluginDomain domain))
            {
                Domain = domain;
            }
            else
            {
                Domain = new PluginDomain(plugin_folder);
                Domains.TryAdd(plugin_folder, Domain);
            }
        }

        public string Available
        {
            get
            {
                return Domain.ClassNames;
            }
        }

        public string Current
        {
            get
            {
                return string.Join(", ", Plugins.Keys);
            }
        }

        public virtual Plugin Add(string plugin, params object[] args)
        {
            var plugin_obj = Domain.Create(plugin, args) as Plugin;
            if (plugin_obj != null && Plugins.TryAdd(plugin, plugin_obj))
            {
                Domain.Unloaded += (sender, domain) => Remove(plugin);
                return plugin_obj;
            }

            return null;
        }

        public virtual Plugin Get(string plugin)
        {
            Plugins.TryGetValue(plugin, out Plugin plugin_obj);
            return plugin_obj;
        }

        public virtual bool Remove(string plugin)
        {
            if (Plugins.TryRemove(plugin, out Plugin plugin_obj))
            {
                (plugin_obj as IDisposable)?.Dispose();
                return true;
            }

            return false;
        }

        public virtual void Dispose()
        {
            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    (plugin as IDisposable)?.Dispose();
                }
                catch (RemotingException)
                {
                }
            }
            Plugins.Clear();
        }

        static public string ClassNames(string plugin_folder)
        {
            if (Domains.TryGetValue(plugin_folder, out PluginDomain domain))
            {
                return domain.ClassNames;
            }

            return null;
        }

        static public string Load(string plugin_folder)
        {
            if (!Domains.TryGetValue(plugin_folder, out PluginDomain domain))
            {
                domain = new PluginDomain(plugin_folder);
                Domains.TryAdd(plugin_folder, domain);
            }

            domain.Load(typeof(Plugin));
            return domain.ClassNames;
        }

        static public void Unload(string plugin_folder)
        {
            if (Domains.TryGetValue(plugin_folder, out PluginDomain domain))
            {
                domain.Unload();
            }
        }
    }
}
