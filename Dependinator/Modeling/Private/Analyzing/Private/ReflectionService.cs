using System;
using System.Threading.Tasks;

namespace Dependinator.Modeling.Private.Analyzing.Private
{
	internal class ReflectionService : IReflectionService
	{
		private readonly Lazy<IModelNotifications> modelNotifications;

		public ReflectionService(Lazy<IModelNotifications> modelNotifications)
		{
			this.modelNotifications = modelNotifications;
		}


		public Task AnalyzeAsync(string assemblyPath)
		{
			return Task.Run(() =>
			{
				// To avoid locking files when loading them for reflection, a separate AppDomain is created
				// where the reflection can be done. This domain can then be unloaded
				AppDomain reflectionDomain = CreateAppDomain();
				try
				{
					Analyzer analyzer = CreateTypeInDomain<Analyzer>(reflectionDomain);

					// To send notifications from sub domain, we use a receiver in this domain, which is
					// passed to the sub-domain		
					NotificationReceiver receiver = new NotificationReceiver(modelNotifications.Value);

					analyzer.AnalyzeAssembly(assemblyPath, receiver);
				}
				finally
				{
					AppDomain.Unload(reflectionDomain);
				}
			});
		}


		private static T CreateTypeInDomain<T>(AppDomain reflectionDomain)
		{
			return (T)reflectionDomain.CreateInstanceAndUnwrap(
				typeof(T).Assembly.FullName, typeof(T).FullName);
		}


		private static AppDomain CreateAppDomain()
		{
			return AppDomain.CreateDomain(Guid.NewGuid().ToString());
		}
	}
}