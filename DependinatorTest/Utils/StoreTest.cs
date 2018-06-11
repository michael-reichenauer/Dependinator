using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dependinator.Utils.Serialization;
using Dependinator.Utils.Threading;
using NUnit.Framework;


namespace DependinatorTest.Utils
{
	[TestFixture]
	public class StoreTest
	{
		private readonly string dbDir = Path.Combine(Path.GetTempPath(), "StoreTest");

		private readonly SHA1 hash = SHA1.Create();


		[Test]
		public void TestWrite()
		{
			Store.Delete(dbDir);

			int count = 1000000;
			Timing t = Timing.Start();

			List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>(count);
			for (int i = 0; i < count; i++)
			{
				string guid = Guid.NewGuid().ToString();
				string data = guid + Guid.NewGuid() + Guid.NewGuid();
				string key = ToKey(data);
				items.Add(new KeyValuePair<string, string>(key, data));
			}
			t.Log($"Created {count} values");


			using (Store<string, string> store = new Store<string, string>(dbDir))
			{
				t = Timing.Start();
				foreach (var pair in items)
				{

					store.Set(pair.Key, pair.Value);
				}

				t.Log($"Added to cache {count} items");
			}

			t.Log($"Wrote {count} items");
		}


		private string ToKey(string value)
		{
			byte[] dataBytes = Encoding.UTF8.GetBytes(value);
			byte[] hashBytes = hash.ComputeHash(dataBytes);
			return hashBytes.ToHex();
		}


		[Test]
		public void TestRead()
		{
			IReadOnlyList<KeyValuePair<string, string>> pairs;

			Timing t = Timing.Start();
			using (Store<string, string> store = new Store<string, string>(dbDir))
			{
				pairs = store.GetAll();
			}

			t.Log($"Read {pairs.Count} items");
		}
	}
}