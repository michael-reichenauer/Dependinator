using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class XmlDocParser
	{
		private readonly string assemblyPath;
		private readonly Lazy<IReadOnlyDictionary<string, string>> descriptions;


		public XmlDocParser(string assemblyPath)
		{
			this.assemblyPath = assemblyPath;
			descriptions = new Lazy<IReadOnlyDictionary<string, string>>(GetDescriptions);
		}


		public string GetDescription(string nodeName)
		{
			if (descriptions.Value.TryGetValue(nodeName, out string description))
			{
				return description;
			}

			return null;
		}


		private IReadOnlyDictionary<string, string> GetDescriptions()
		{
			Dictionary<string, string> items = new Dictionary<string, string>();
			try
			{
				string directoryName = Path.GetDirectoryName(assemblyPath);
				string xmlFileName = Path.GetFileNameWithoutExtension(assemblyPath) + ".xml";
				string xmlFilePath = Path.Combine(directoryName, xmlFileName);

				if (File.Exists(xmlFilePath))
				{
					XDocument doc = XDocument.Load(xmlFilePath);

					string assemblyName = GetAssemblyName(doc);

					var members = doc.Descendants("member");
					foreach (var member in members)
					{
						string memberName = GetMemberName(member);
						string summary = GetSummary(member);

						Add(items, assemblyName, memberName, summary);
						Console.WriteLine(summary);
					}
				}
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to parse xml docs for {assemblyPath}");
			}

			return items;
		}


		private void Add(
			IDictionary<string, string> items,
			string assemblyName,
			string memberName,
			string summary)
		{
			// Trim type marker "M:" and "T:" in member name.
			memberName = memberName.Substring(2);
			string fullName = memberName;

			if (!string.IsNullOrEmpty(assemblyName))
			{
				fullName = $"?{assemblyName}.{memberName}";
			}

			items.Add(fullName, summary);
		}


		private static string GetSummary(XElement member)
		{
			string summary = member.Descendants("summary").FirstOrDefault()?.Value;

			return summary?.Trim();
		}


		private static string GetMemberName(XElement member)
		{
			string memberName = member.Attribute("name")?.Value;

			return memberName?.Trim();
		}


		private static string GetAssemblyName(XDocument doc)
		{
			string assemblyName = doc
				.Descendants("assembly")
				.Descendants("name").FirstOrDefault()?.Value;

			return assemblyName;
		}
	}
}