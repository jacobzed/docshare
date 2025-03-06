using System.Collections.Concurrent;
using System.Text.Json;

namespace DocShare.Helpers
{
    /// <summary>
    /// Keeps track of all connected clients and handles messaging.
    /// Messages must follow SSE syntax: 
    /// https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events#fields
    /// </summary>
    public class ClientManager
    {
        record Client(StreamWriter Writer)
        {
            public Guid Id = Guid.NewGuid();
            public SemaphoreSlim Lock = new(1, 1);
        }

        private readonly ConcurrentDictionary<Guid, Client> _clients = new();

        private readonly SemaphoreSlim _lock = new(1, 1);

        public int ClientCount => _clients.Count;

        public Guid AddClient(StreamWriter writer)
        {
            var client = new Client(writer);
            _clients.TryAdd(client.Id, client);
            return client.Id;
        }

        public void RemoveClient(Guid id)
        {
            _clients.TryRemove(id, out _);
        }

        public async Task BroadcastMessageAsync(Object message)
        {
            var disconnected = new List<Guid>();

            foreach (var (id, client) in _clients)
            {
                await client.Lock.WaitAsync();
                try
                {
                    await client.Writer.WriteAsync("data: ");
                    await client.Writer.WriteAsync(JsonSerializer.Serialize(message));
                    await client.Writer.WriteAsync("\n\n");
                    await client.Writer.FlushAsync();
                }
                catch
                {
                    // If writing fails, mark the client for removal
                    disconnected.Add(id);
                }
                finally
                {
                    client.Lock.Release();
                }
            }

            // Remove disconnected clients
            foreach (var clientId in disconnected)
            {
                RemoveClient(clientId);
            }
        }

        public async Task SendMessageAsync(Guid id, Object message)
        {
            if (_clients.TryGetValue(id, out var client))
            {
                await client.Lock.WaitAsync();
                try
                {
                    await client.Writer.WriteAsync("data: ");
                    await client.Writer.WriteAsync(JsonSerializer.Serialize(message));
                    await client.Writer.WriteAsync("\n\n");
                    await client.Writer.FlushAsync();
                }
                catch
                {
                    RemoveClient(id);
                }
                finally
                {
                    client.Lock.Release();
                }
            }
        }

    }
}
