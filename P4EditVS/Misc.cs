using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace P4EditVS
{
    static class Misc
    {
        //########################################################################
        //########################################################################

        public static string GetPathFileNameWithoutExtension(string path)
        {
            try
            {
                return Path.GetFileNameWithoutExtension(path);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        //########################################################################
        //########################################################################

        public static string GetPathExtension(string path)
        {
            try
            {
                return Path.GetExtension(path);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        //########################################################################
        //########################################################################

        public static string GetPathDirectoryName(string path)
        {
            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        //########################################################################
        //########################################################################

        public static string GetPathRoot(string path)
        {
            try
            {
                return Path.GetPathRoot(path);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static bool IsFileReadOnly(string path)
        {
            try
            {
                System.IO.FileInfo info = new System.IO.FileInfo(path);
                return info.IsReadOnly;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        //########################################################################
        //########################################################################

        public static readonly Encoding defaultXmlEncoding = new UTF8Encoding(false);//false=no BOM

        //########################################################################
        //########################################################################

        public static T LoadXml<T>(string fileName) where T : class
        {
            if (fileName == null) return null;

            using (XmlReader reader = XmlReader.Create(fileName)) return DeserializeXml<T>(reader);
        }

        public static T ReadXml<T>(Stream stream) where T : class
        {
            using (var reader = XmlReader.Create(stream)) return DeserializeXml<T>(reader);
        }

        private static T DeserializeXml<T>(XmlReader reader) where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            return serializer.Deserialize(reader) as T;
        }

        //########################################################################
        //########################################################################

        public static T LoadXmlOrCreateDefault<T>(string fileName) where T : class, new()
        {
            bool outCreatedDefault;
            return DoXmlOrCreateDefault<T>(() => LoadXml<T>(fileName), out outCreatedDefault);
        }

        public static T ReadXmlOrCreateDefault<T>(Stream stream) where T : class, new()
        {
            bool outCreatedDefault;
            return DoXmlOrCreateDefault<T>(() => ReadXml<T>(stream), out outCreatedDefault);
        }

        public static T LoadXmlOrCreateDefault<T>(string fileName, out bool outCreatedDefault) where T : class, new()
        {
            return DoXmlOrCreateDefault<T>(() => LoadXml<T>(fileName), out outCreatedDefault);
        }

        public static T ReadXmlOrCreateDefault<T>(Stream stream, out bool outCreatedDefault) where T : class, new()
        {
            return DoXmlOrCreateDefault<T>(() => ReadXml<T>(stream), out outCreatedDefault);
        }

        private static T DoXmlOrCreateDefault<T>(Func<T> reader, out bool outCreatedDefault) where T : class, new()
        {
            T result = null;

            try
            {
                result = reader();
            }
            catch (InvalidOperationException)
            {
            }
            catch (DirectoryNotFoundException)
			{

			}
            catch (FileNotFoundException)
			{
            }

            outCreatedDefault = false;
            if (result == null)
            {
                result = new T();
                outCreatedDefault = true;
            }

            return result;
        }

        //########################################################################
        //########################################################################

        public static void WriteXml<T>(Stream stream, T data)
        {
            DoWriteXml((XmlWriterSettings settings) => XmlWriter.Create(stream, settings), data);
        }

        public static void SaveXml<T>(string fileName, T data)
        {
            if (fileName == null) return;

			try
			{
				DoWriteXml((XmlWriterSettings settings) => XmlWriter.Create(fileName, settings), data);
			}
			catch(DirectoryNotFoundException)
			{
				Directory.CreateDirectory(GetPathDirectoryName(fileName));
				DoWriteXml((XmlWriterSettings settings) => XmlWriter.Create(fileName, settings), data);
			}
		}

        private static void DoWriteXml<T>(Func<XmlWriterSettings, XmlWriter> createWriter, T data)
        {
            XmlWriterSettings settings = new XmlWriterSettings();

            settings.Encoding = defaultXmlEncoding;
            settings.Indent = true;

            using (XmlWriter writer = createWriter(settings))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(writer, data);
            }
        }

        //########################################################################
        //########################################################################
    }
}
