using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NuGetClientHelper;

namespace Slnx
{
    public static class SlnxHandlerExtensions
    {
        public static IEnumerable<Uri> SelectAllSources(this IEnumerable<NuGetPackage> list)
        {
            return list.SelectMany(
                              p =>
                                  {
                                      var l = new List<Uri>();
                                      l.Add(p.Source);
                                      if (p.DependencySources != null) l.AddRange(p.DependencySources);
                                      return l;
                                  }
                        );
        }

        public static XmlNode CreateNugetConfigSource(this Uri uri, XmlDocument xml)
        {
            var source = xml.CreateNode(XmlNodeType.Element, "add", null);
            source.Attributes.Append(xml.CreateAttribute("key")).InnerText = uri.IsFile ? Path.GetFileNameWithoutExtension(uri.LocalPath) : uri.Host;
            source.Attributes.Append(xml.CreateAttribute("value")).Value = uri.ToString();
            return source;
        }
    }
}
