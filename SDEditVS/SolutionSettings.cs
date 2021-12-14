using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SDEditVS
{


	public class SolutionSettings
	{
		// Increment version and add patch code to Load() when changing Settings struct
		private static readonly int _currentVersion = 0;
		public class Settings
		{
			public int Version = _currentVersion;
			public int SelectedWorkspace;

			public Settings Copy()
			{
				return (Settings)this.MemberwiseClone();
			}
		}

		Settings _settings;
		public int SelectedWorkspace { get => _settings.SelectedWorkspace; set => _settings.SelectedWorkspace = value; }

		readonly string _path;
		readonly string _fileName;
		readonly string _pathAndFileName;
		Task _saveTask;

		public string PathAndFileName { get => _pathAndFileName;  }

		public SolutionSettings(string dataDirectory, string solutionPathAndFileName)
		{
			_path = dataDirectory;

			// Build filename in form <solution_name>_<hash_of_solution_path_and_file_name>.xml
			string solutionName = Misc.GetPathFileNameWithoutExtension(solutionPathAndFileName);
			string hash = GetHash(solutionPathAndFileName.ToCharArray()).ToString("X"); // Use hex value to avoid MAX_PATH issues
			_fileName = solutionName + "_" + hash + ".xml";
			_pathAndFileName = _path + _fileName;

        }


		public bool DoesExist()
		{
			return File.Exists(PathAndFileName);
		}

		public void Create()
		{
			_settings = Misc.LoadXmlOrCreateDefault<Settings>(PathAndFileName);
		}

		public void Load()
		{
			_settings = Misc.LoadXmlOrCreateDefault<Settings>(PathAndFileName);
			if(_settings.Version != _currentVersion)
			{
				// Add patch code here
			}
		}

		public void Save()
		{
			WaitForSaveTask();
			Save(PathAndFileName, _settings);
		}

		public void SaveAsync()
		{
			Settings settings = _settings.Copy();
			string pathAndFileName = String.Copy(PathAndFileName);

			WaitForSaveTask();

			_saveTask = Task.Run(() =>
			{
				Save(pathAndFileName, settings);
			});
		}

		private void Save(string pathAndFileName, Settings settings)
		{
			Misc.SaveXml<Settings>(pathAndFileName, settings);
		}

		private void WaitForSaveTask()
		{
			if (_saveTask != null)
			{
				while (!_saveTask.IsCompleted) { }
			}
		}

        private uint GetHash(char[] data)
        {
            uint hash = 2166136261; // FNV Offset Basis

            foreach (char c in data)
            {

                hash = hash ^ c;
                hash = hash * 16777619; // FNV prime
            }

            return hash;
        }
	}
}
