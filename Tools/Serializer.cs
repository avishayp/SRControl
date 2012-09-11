using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace Tools
{
    public static class Serializer
    {
        public static void Save<T>(T t, string path)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(t.GetType());
            using (FileStream fs = File.Create(path))
            {
                xmlSerializer.Serialize(fs, t);
            }
        }

        public static T Load<T>(string path)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (FileStream fs = File.OpenRead(path))
            {
                return (T)xmlSerializer.Deserialize(fs);
            }
        }
    }
}
