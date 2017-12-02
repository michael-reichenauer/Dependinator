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
			memberName = memberName.Substring(2)
				.Replace("``", "`");

			int index1 = memberName.IndexOf('(');
			int index2 = memberName.IndexOf(')');

			if (index1 > -1 && index2 > (index1 + 1))
			{
				string parameters = memberName
					.Substring(index1 + 1, (index2 - index1) - 1)
					.Replace(".", "#");

				memberName = $"{memberName.Substring(0, index1)}({parameters})";
			}

			string fullName = memberName;

			if (!string.IsNullOrEmpty(assemblyName))
			{
				fullName = $"?{assemblyName}.{memberName}";
			}

			items.Add(fullName, summary);
		}


		private static string GetSummary(XElement member)
		{
			XElement node = member.Descendants("summary").FirstOrDefault();
			
			string summary = node?.ToString();

			if (!string.IsNullOrEmpty(summary))
			{
				summary = summary
					.Replace("<summary>", "")
					.Replace("</summary>", "")
					.Replace("<see cref=\"T:", "")
					.Replace("<see cref=\"M:", "")
					.Replace("<see cref=\"P:", "")
					.Replace("<see cref=\"F:", "")
					.Replace("<see cref=\"F:", "")
					.Replace("\" />", "");

				summary = string.Join("\n", summary.Split("\n".ToCharArray()).Select(line => line.Trim()));
			}

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