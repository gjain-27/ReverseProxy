namespace Proxy.Caching
{
    public class LruCache
    {
        private readonly long _capacityBytes;
        private readonly TimeProvider _time;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map = new();
        private readonly LinkedList<CacheEntry> _order = new();
        private readonly object _sync = new();
        private long _sizeBytes;

        public LruCache(long capacityBytes, TimeProvider time)
        {
            _capacityBytes = capacityBytes;
            _time = time;
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _map.Count;
                }
            }
        }

        public long SizeBytes
        {
            get
            {
                lock (_sync)
                {
                    return _sizeBytes;
                }
            }
        }

        public bool TryGet(string key, out CachedResponse? response)
        {
            lock (_sync)
            {
                response = null;

                if (!_map.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
                {
                    return false;
                }

                CachedResponse cached = node.Value.Response;
                if (_time.GetElapsedTime(cached.StoredAt) >= cached.Ttl)
                {
                    Remove(node);
                    return false;
                }

                _order.Remove(node);
                _order.AddFirst(node);

                response = cached;
                return true;
            }
        }

        public void Set(string key, CachedResponse response)
        {
            lock (_sync)
            {
                if (response.SizeBytes > _capacityBytes)
                {
                    return;
                }

                if (_map.TryGetValue(key, out LinkedListNode<CacheEntry>? existing))
                {
                    Remove(existing);
                }

                LinkedListNode<CacheEntry> node = _order.AddFirst(new CacheEntry(key, response));
                _map[key] = node;
                _sizeBytes += response.SizeBytes;

                while (_sizeBytes > _capacityBytes)
                {
                    Remove(_order.Last!);
                }
            }
        }

        private void Remove(LinkedListNode<CacheEntry> node)
        {
            _order.Remove(node);
            _map.Remove(node.Value.Key);
            _sizeBytes -= node.Value.Response.SizeBytes;
        }

        private class CacheEntry
        {
            public string Key { get; }
            public CachedResponse Response { get; }

            public CacheEntry(string key, CachedResponse response)
            {
                Key = key;
                Response = response;
            }
        }
    }
}
