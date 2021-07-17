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

        // IsSubPathOf(), WithEnding() and Right() come from https://stackoverflow.com/questions/5617320/given-full-path-check-if-path-is-subdirectory-of-some-other-path-or-otherwise/31941159#31941159
        // courtesy of @angularsen

        /// <summary>
        /// Returns true if <paramref name="path"/> starts with the path <paramref name="baseDirPath"/>.
        /// The comparison is case-insensitive, handles / and \ slashes as folder separators and
        /// only matches if the base dir folder name is matched exactly ("c:\foobar\file.txt" is not a sub path of "c:\foo").
        /// </summary>
        public static bool IsSubPathOf(this string path, string baseDirPath)
        {
            // We do these beforehand, so skip duplicating it
            //string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
            //    .WithEnding("\\"));
            //string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
            //    .WithEnding("\\"));
            return path.StartsWith(baseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeFilePath(this string path)
        {
            return Path.GetFullPath(path.Trim().Replace('/', '\\').ToLower());
        }

        public static string NormalizeDirectoryPath(this string path)
        {
            return Path.GetFullPath(path.Trim().Replace('/', '\\').ToLower().WithEnding("\\"));
        }

        /// <summary>
        /// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
        /// results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
        public static string WithEnding(this string str, string ending)
        {
            if (str == null)
                return ending;

            string result = str;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (int i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                    return tmp;
            }

            return result;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        public static string Right(this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length is less than zero");
            }

            return (length < value.Length) ? value.Substring(value.Length - length) : value;
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
