using System;

namespace Dependinator.Common.SettingsHandling
{
	internal interface ISettingsService
	{
		void EnsureExists<T>() where T : class;

		void Edit<T>(Action<T> editAction) where T : class;
		void Edit<T>(string path, Action<T> editAction) where T : class;

		T Get<T>() where T:class;
		T Get<T>(string path) where T : class;

		void Set<T>(T setting) where T : class;
		void Set<T>(string path, T setting) where T : class;

		string GetFilePath<T>() where T : class;
	}
}