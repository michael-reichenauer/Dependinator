using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core.Activators.Reflection;


namespace Dependinator.Utils.Dependencies
{
    internal class DependencyInjection
    {
        private IContainer container;


        public T Resolve<T>()
        {
            Asserter.NotNull(container);

            return container.Resolve<T>();
        }


        public void RegisterTypes(Assembly assembly)
        {
            try
            {
                ContainerBuilder builder = new ContainerBuilder();

                // Need to make Autofac find also "internal" constructors e.g. windows dialogs
                DefaultConstructorFinder constructorFinder = new DefaultConstructorFinder(
                    type => type.GetConstructors(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

                // Register single instance types
                builder.RegisterAssemblyTypes(assembly)
                    .Where(IsSingleInstance)
                    .FindConstructorsWith(constructorFinder)
                    .AsSelf()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .OwnedByLifetimeScope();

                // Register non single instance types
                builder.RegisterAssemblyTypes(assembly)
                    .Where(t => !IsSingleInstance(t))
                    .FindConstructorsWith(constructorFinder)
                    .AsSelf()
                    .AsImplementedInterfaces()
                    .OwnedByLifetimeScope();

                container = builder.Build();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to register types {e}");
                Asserter.FailFast(e);
                throw;
            }
        }


        private static bool IsSingleInstance(Type type)
        {
            // All types that are marked with the "SingleInstance" attribute
            return type.GetCustomAttributes(false).FirstOrDefault(
                       obj => obj.GetType().Name == "SingleInstanceAttribute") != null;
        }
    }
}
