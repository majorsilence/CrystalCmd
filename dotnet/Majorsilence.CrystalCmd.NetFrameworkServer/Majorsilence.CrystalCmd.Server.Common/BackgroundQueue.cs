using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public class BackgroundQueue
    {
        private ConcurrentQueue<Action> _theQueueAction = new ConcurrentQueue<Action>();
        private bool threadStarted = false;

        public void QueueThread(Action work)
        {
            _theQueueAction.Enqueue(work);

            if (threadStarted == false)
            {
                threadStarted = true;
                var t = new Thread(DoThreadness);
                t.Start();
            }
        }

        private void DoThreadness()
        {
            while (true)
            {
                const int oneSecond = 1000;
                const int tenSeconds = oneSecond * 10;
                var totalTimeEmpty = 0;
                while (_theQueueAction.IsEmpty)
                {
                    var timeAsleep = oneSecond;
                    Thread.Sleep(timeAsleep);
                    totalTimeEmpty += timeAsleep;

                    if (totalTimeEmpty >= tenSeconds || _isDisposed)
                    {
                        threadStarted = false;
                        return;
                    }
                }

                while (_theQueueAction.TryDequeue(out Action work))
                {
                    work();
                }
            }
        }

        public static BackgroundQueue Instance
        {
            get
            {
                return InstanceDict("default_key");
            }
        }

        private static ConcurrentDictionary<string, BackgroundQueue> dictQueue = new ConcurrentDictionary<string, BackgroundQueue>();
        public static BackgroundQueue InstanceDict(string key)
        {

            if (!dictQueue.ContainsKey(key))
            {
                dictQueue.AddOrUpdate(key, new BackgroundQueue(), (k, o) => o);
            }

            return dictQueue[key];

        }

        public static void DisposeDict()
        {
            if (dictQueue != null)
                foreach (var queue in dictQueue.Values)
                    queue?.Dispose();
        }

        private bool _isDisposed = false;
        private void Dispose()
        {
            _isDisposed = true;
        }
    }
}
