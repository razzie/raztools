﻿/*
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
using System.IO;
using System.Linq;
using System.Reflection;

namespace raztools
{
    public class PluginDomain
    {
        public event EventHandler<AppDomain> Unloaded;

        static private string AppFolder { get; } = AppDomain.CurrentDomain.BaseDirectory;
        private AppDomain Domain { get; set; }
        private RemoteLoader Loader { get; set; }
        private string Folder { get; set; }
        private Dictionary<string, string> Assemblies { get; } = new Dictionary<string, string>();
        public RemoteClass[] Classes { get; private set; } = new RemoteClass[0];

        private FileInfo[] DLLs
        {
            get
            {
                return new DirectoryInfo(Folder).GetFiles("*.dll");
            }
        }

        public string ClassNames
        {
            get
            {
                return string.Join(", ", Classes.Select(c => c.TypeNme).ToArray());
            }
        }

        public PluginDomain(string folder)
        {
            Folder = folder;
            CreateDomain();
        }

        public MarshalByRefObject Create(string typename, params object[] args)
        {
            foreach (var rclass in Classes)
            {
                if (rclass.TypeNme.Equals(typename))
                {
                    return Loader.CreateInstance(rclass, args);
                }
            }

            return null;
        }

        static private FileInfo FindDLL(string dir, AssemblyName assembly)
        {
            string dll = assembly.Name + ".dll";
            return new DirectoryInfo(dir).GetFiles().FirstOrDefault(f => f.Name.Equals(dll, StringComparison.InvariantCultureIgnoreCase));
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var dll = FindDLL(Folder, new AssemblyName(args.Name));
            if (dll != null)
            {
                return Assembly.LoadFile(dll.FullName);
            }

            return null;
        }

        private void CreateDomain()
        {
            if (Domain != null) return;

            var setup = new AppDomainSetup()
            {
                ShadowCopyFiles = "false",
                //CachePath = AppFolder,
                //ShadowCopyDirectories = AppFolder,
                ApplicationBase = AppFolder,
                PrivateBinPath = AppFolder
            };

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Domain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), AppDomain.CurrentDomain.Evidence, setup, Assembly.GetExecutingAssembly().PermissionSet);
            Loader = (RemoteLoader)Domain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(RemoteLoader).FullName);
            Loader.Folder = Folder;
        }

        public void Load(Type baseclass)
        {
            CreateDomain();

            var classes = new List<RemoteClass>(Classes);
            foreach (var file in DLLs)
            {
                var aname = AssemblyName.GetAssemblyName(file.FullName);
                if (Assemblies.TryGetValue(file.FullName, out string cached_aname))
                {
                    if (!aname.FullName.Equals(cached_aname))
                    {
                        Unload();
                        Load(baseclass);
                        return;
                    }
                }
                else
                {
                    classes.AddRange(Loader.LoadAssemblyClasses(aname));
                    Assemblies.Add(file.FullName, aname.FullName);
                }
            }

            if (baseclass == null)
                Classes = classes.ToArray();
            else
                Classes = Loader.FilterClasses(classes.ToArray(), baseclass.FullName);
        }

        public void Unload()
        {
            if (Domain != null)
            {
                Unloaded?.Invoke(this, Domain);

                AppDomain.Unload(Domain);
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                Domain = null;
            }

            Unloaded = null;

            Assemblies.Clear();
            Classes = new RemoteClass[0];
        }
    }
}
