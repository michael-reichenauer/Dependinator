using System;
using System.Reflection;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.SolutionFileParsing.Private
{
	/// <summary>
	/// Method used to extract information from internal Microsoft classes using reflection
	/// </summary>
	internal static class Reflection
	{

		public static Type GetType(Assembly assembly, string typeName)
		{
			var type = assembly.GetType(typeName);

			if (type == null)
			{
				throw new Exception("typeName");
			}

			return type;
		}


		public static object Create(Type type, params object[] parameters)
		{
			ConstructorInfo constructor = type.GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null, Type.EmptyTypes, null);

			return constructor.Invoke(parameters);
		}


		public static T Invoke<T>(this object instance, string name, params object[] parameters)
		{
			MethodInfo method = GetMethod(instance, name);
			object returnValue = method.Invoke(instance, parameters);

			return (T)returnValue;
		}

		public static void Invoke(this object instance, string name, params object[] parameters)
		{
			MethodInfo method = GetMethod(instance, name);

			method.Invoke(instance, parameters);
		}


		public static T GetProperty<T>(this object instance, string name)
		{
			PropertyInfo property = GetProperty(instance, name);

			object value = property.GetValue(instance);

			return (T)value;
		}

		public static T GetField<T>(this object instance, string name)
		{
			FieldInfo field = GetField(instance, name);

			object value = field.GetValue(instance);

			return (T)value;
		}

		public static void SetProperty(this object instance, string name, object value)
		{
			PropertyInfo property = GetProperty(instance, name);

			property.SetValue(instance, value);
		}



		private static MethodInfo GetMethod(object instance, string name)
		{
			return instance.GetType()
				.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
		}


		private static PropertyInfo GetProperty(object instance, string name)
		{
			return instance.GetType()
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
		}

		private static FieldInfo GetField(object instance, string name)
		{
			return instance.GetType()
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
		}
	}
}