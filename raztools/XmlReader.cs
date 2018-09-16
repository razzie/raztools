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
using System.Dynamic;
using System.IO;
using System.Xml;

namespace raztools
{
    public static class XmlReader
    {
        class DynamicXmlNode : DynamicObject
        {
            private XmlNode m_node;

            public DynamicXmlNode(XmlNode node)
            {
                m_node = node;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                var children = m_node.ChildNodes as IEnumerable<XmlNode>;
                foreach (var child in children)
                {
                    if (child.Name == binder.Name)
                    {
                        result = new DynamicXmlNode(child);
                        return true;
                    }
                }

                var attribute = m_node.Attributes[binder.Name];
                if (attribute != null)
                {
                    result = attribute.Value;
                    return true;
                }

                result = null;
                return false;
            }

            public override bool TrySetMember(SetMemberBinder binder, object value)
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return m_node.Value;
            }
        }

        static public dynamic Read(string xml_file)
        {
            var doc = new XmlDocument();
            doc.Load(xml_file);
            return new DynamicXmlNode(doc);
        }

        static public dynamic Read(Stream xml_stream)
        {
            var doc = new XmlDocument();
            doc.Load(xml_stream);
            return new DynamicXmlNode(doc);
        }

        static public dynamic Read(XmlNode node)
        {
            return new DynamicXmlNode(node);
        }
    }
}
