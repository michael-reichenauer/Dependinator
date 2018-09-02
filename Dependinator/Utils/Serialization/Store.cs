////using System;
////using System.Collections.Concurrent;
////using System.Collections.Generic;
////using System.Linq;
////using System.Threading;
////using System.Threading.Tasks;
////using Microsoft.Isam.Esent.Collections.Generic;


////namespace Dependinator.Utils.Serialization
////{
////    internal static class Store
////    {
////        public static void Delete(string directoryPath)
////        {
////            if (PersistentDictionaryFile.Exists(directoryPath))
////            {
////                PersistentDictionaryFile.DeleteFiles(directoryPath);
////            }
////        }
////    }


////    internal class Store<TKey, TValue> : IDisposable where TKey : IComparable<TKey>
////    {
////        private readonly Dictionary<TKey, TValue> cache = new Dictionary<TKey, TValue>();
////        private readonly AutoResetEvent done = new AutoResetEvent(false);
////        private readonly PersistentDictionary<TKey, TValue> persistentDictionary;

////        private readonly BlockingCollection<KeyValuePair<TKey, TValue>> persistentQueue =
////            new BlockingCollection<KeyValuePair<TKey, TValue>>();

////        private readonly Task writeTask;


////        public Store(string directoryPath)
////        {
////            persistentDictionary = new PersistentDictionary<TKey, TValue>(directoryPath);

////            writeTask = Task.Run(() => ProcessPersistentQueue());
////        }


////        public void Dispose()
////        {
////            persistentQueue.CompleteAdding();
////            done.WaitOne();

////            persistentDictionary.Flush();

////            persistentQueue.Dispose();
////            persistentQueue.Dispose();

////            cache.Clear();
////        }


////        private void ProcessPersistentQueue()
////        {
////            while (persistentQueue.TryTake(out var pair, -1))
////            {
////                persistentDictionary[pair.Key] = pair.Value;
////            }

////            done.Set();
////        }


////        public void Set(TKey key, TValue value)
////        {
////            cache[key] = value;
////            persistentQueue.Add(new KeyValuePair<TKey, TValue>(key, value));
////        }


////        public bool TryGet(TKey key, out TValue value)
////        {
////            if (cache.TryGetValue(key, out value))
////            {
////                return true;
////            }

////            if (persistentDictionary.TryGetValue(key, out value))
////            {
////                cache[key] = value;
////                return true;
////            }

////            return false;
////        }


////        public IReadOnlyList<KeyValuePair<TKey, TValue>> GetAll()
////        {
////            return persistentDictionary.ToList();
////        }


////        public async Task CloseAsync()
////        {
////            persistentQueue.CompleteAdding();
////            await writeTask;
////            await Task.Run(() => persistentDictionary.Flush());
////        }
////    }
////}
