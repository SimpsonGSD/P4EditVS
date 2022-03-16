using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;


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

        //########################################################################
        //########################################################################

        public static string GetPathFileName(string path)
        {
            try
            {
                return Path.GetFileName(path); 
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        //########################################################################
        //########################################################################


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetLongPathName(string ShortPath, StringBuilder sb, int buffer);

        [DllImport("kernel32.dll")]
        static extern uint GetShortPathName(string longpath, StringBuilder sb, int buffer);

        /// <summary>
        /// THIS DOES NOT WORK AS INTENDED. Returns case sensitive path of <paramref name="path"/>
        /// Taken from https://stackoverflow.com/questions/4763117/how-can-i-obtain-the-case-sensitive-path-on-windows
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetWindowsPhysicalPath(string path)
        {
            StringBuilder builder = new StringBuilder(255);

            // names with long extension can cause the short name to be actually larger than
            // the long name.
            GetShortPathName(path, builder, builder.Capacity);

            path = builder.ToString();

            uint result = GetLongPathName(path, builder, builder.Capacity);

            if (result > 0 && result < builder.Capacity)
            {
                //Success retrieved long file name
                builder[0] = char.ToLower(builder[0]);
                return builder.ToString(0, (int)result);
            }

            if (result > 0)
            {
                //Need more capacity in the buffer
                //specified in the result variable
                builder = new StringBuilder((int)result);
                builder[0] = char.ToLower(builder[0]);
                result = GetLongPathName(path, builder, builder.Capacity);
                return builder.ToString(0, (int)result);
            }

            return null;
        }

        //########################################################################
        //########################################################################

        /// <summary>
        /// Returns case sensitive path part of <paramref name="path"/>
        /// Taken from https://stackoverflow.com/questions/4763117/how-can-i-obtain-the-case-sensitive-path-on-windows
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCaseSensitivePath(string path)
        {
            var root = Path.GetPathRoot(path);
            try
            {
                foreach (var name in path.Substring(root.Length).Split(Path.DirectorySeparatorChar))
                    root = Directory.GetFileSystemEntries(root, name).First();
            }
            catch (Exception)
            {
                // Log("Path not found: " + path);
                root += path.Substring(root.Length);
            }
            int separator_index = root.LastIndexOf(Path.DirectorySeparatorChar);
            return root.Substring(0, separator_index);
        }

        //########################################################################
        //########################################################################

        /// <summary>
        /// Returns case sensitive filename part of <paramref name="path"/>
        /// Taken from https://stackoverflow.com/questions/4763117/how-can-i-obtain-the-case-sensitive-path-on-windows
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCaseSensitiveFileName(string path)
        {
            string fullpath = Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path)).FirstOrDefault();
            int separator_index = fullpath.LastIndexOf(Path.DirectorySeparatorChar) + 1;
            return fullpath.Substring(separator_index, fullpath.Length - separator_index);
        }

        //########################################################################
        //########################################################################

        /// <summary>
        /// Returns case sensitive path of <paramref name="path"/>
        /// Taken from https://stackoverflow.com/questions/4763117/how-can-i-obtain-the-case-sensitive-path-on-windows
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCaseSensitiveFilePath(string path)
        {
            return GetCaseSensitivePath(path) + "\\" + GetCaseSensitiveFileName(path);
        }

        //########################################################################
        //########################################################################

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

        //########################################################################
        //########################################################################

        public static string NormalizeFilePath(this string path)
        {
            return Path.GetFullPath(path.Trim().Replace('/', '\\').ToLower());
        }

        //########################################################################
        //########################################################################

        public static string NormalizeDirectoryPath(this string path)
        {
            return Path.GetFullPath(path.Trim().Replace('/', '\\').ToLower().WithEnding("\\"));
        }

        //########################################################################
        //########################################################################

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

        //########################################################################
        //########################################################################

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
