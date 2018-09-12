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
    [Serializable]
    public class RemoteClass
    {
        public string AssemblyName { get; private set; }
        public string Namespace { get; private set; }
        public string TypeNme { get; private set; }

        public RemoteClass(Type type)
        {
            AssemblyName = type.Assembly.GetName().FullName;
            Namespace = type.Namespace;
            TypeNme = type.Name;
        }
    }

    class RemoteLoader : MarshalByRefObject
    {
        public string Folder { get; set; }
        public AppDomain Domain { get { return AppDomain.CurrentDomain; } }
        private Dictionary<string, Assembly> LoadedAssemblies { get; } = new Dictionary<string, Assembly>();

        public RemoteLoader()
        {
            Domain.AssemblyResolve += (sender, args) => LoadAssembly(new AssemblyName(args.Name));
        }

        public MarshalByRefObject CreateInstance(RemoteClass rclass)
        {
            Assembly assembly;
            if (LoadedAssemblies.TryGetValue(rclass.AssemblyName, out assembly))
            {
                return (MarshalByRefObject)assembly.CreateInstance(rclass.Namespace + "." + rclass.TypeNme);
            }
            
            return null;
        }

        public RemoteClass[] LoadAssemblyClasses(AssemblyName aname)
        {
            var assembly = LoadAssembly(aname);
            return assembly.RemoteClasses(typeof(MarshalByRefObject)).ToArray();
        }

        public RemoteClass[] FilterClasses(RemoteClass[] classes, string baseclass_name)
        {
            var baseclass = GetType(baseclass_name);
            var filtered = new List<RemoteClass>();

            foreach (var c in classes)
            {
                Assembly assembly;
                if (LoadedAssemblies.TryGetValue(c.AssemblyName, out assembly))
                {
                    var type = assembly.GetType(c.Namespace + "." + c.TypeNme);
                    if (baseclass.IsAssignableFrom(type))
                        filtered.Add(c);
                }
            }

            return filtered.ToArray();
        }

        static private FileInfo FindDLL(string dir, AssemblyName assembly)
        {
            string dll = assembly.Name + ".dll";
            return new DirectoryInfo(dir).GetFiles().FirstOrDefault(f => f.Name.Equals(dll, StringComparison.InvariantCultureIgnoreCase));
        }

        private FileInfo FindDLL(AssemblyName assembly)
        {
            return FindDLL(Domain.BaseDirectory + Folder, assembly) ?? FindDLL(Domain.BaseDirectory, assembly);
        }

        private Type GetType(string typename)
        {
            foreach (var assembly in LoadedAssemblies.Values)
            {
                var type = assembly.GetType(typename);
                if (type != null)
                    return type;
            }

            return null;
        }

        private Assembly LoadAssembly(AssemblyName assembly)
        {
            if (LoadedAssemblies.ContainsKey(assembly.FullName))
            {
                return LoadedAssemblies[assembly.FullName];
            }

            FileInfo dll = FindDLL(assembly);
            if (dll != null)
            {
                byte[] raw = File.ReadAllBytes(dll.FullName);
                //return LoadDependencies(Assembly.LoadFile(dll.FullName));
                return LoadDependencies(Domain.Load(raw));
            }

            return Domain.Load(assembly);
        }

        private Assembly LoadDependencies(Assembly assembly)
        {
            foreach (var dep in assembly.GetReferencedAssemblies())
            {
                if (!LoadedAssemblies.ContainsKey(dep.FullName))
                {
                    var loaded_dep = LoadAssembly(dep);
                }
            }

            if (!LoadedAssemblies.ContainsKey(assembly.FullName))
            {
                LoadedAssemblies.Add(assembly.FullName, assembly);
            }

            return assembly;
        }
    }

    static class RemoteClassExtensions
    {
        static public IEnumerable<RemoteClass> RemoteClasses(this Assembly assembly, Type basetype)
        {
            return assembly.GetExportedTypes().Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(basetype)).Select(c => new RemoteClass(c));
        }
    }
}
