using DocShare.Helpers;
using System.Text.Json;
using Xunit;

namespace DocShareTests
{
    public class ClientManagerTests
    {
        private class TestWriter : StreamWriter
        {
            private readonly MemoryStream _memoryStream;

            public TestWriter() : base(new MemoryStream())
            {
                _memoryStream = (MemoryStream)BaseStream;
            }

            public string GetWrittenContent()
            {
                _memoryStream.Position = 0;
                using var reader = new StreamReader(_memoryStream, leaveOpen: true);
                return reader.ReadToEnd();
            }

            public void Reset()
            {
                _memoryStream.SetLength(0);
            }
        }

        [Fact]
        public async Task SendMessageAsync_WritesCorrectSSEFormat()
        {
            // Arrange
            var manager = new ClientManager();
            var writer = new TestWriter();
            var id = manager.AddClient(writer);
            var message = new { test = "message" };

            // Act
            await manager.SendMessageAsync(id, message);

            // Assert
            var written = writer.GetWrittenContent();
            var expected = $"data: {JsonSerializer.Serialize(message)}\n\n";
            Assert.Equal(expected, written);
        }

        [Fact]
        public async Task BroadcastMessageAsync_SendsToAllClients()
        {
            // Arrange
            var manager = new ClientManager();
            var writer1 = new TestWriter();
            var writer2 = new TestWriter();
            manager.AddClient(writer1);
            manager.AddClient(writer2);
            var message = new { test = "broadcast" };

            // Act
            await manager.BroadcastMessageAsync(message);

            // Assert
            var expected = $"data: {JsonSerializer.Serialize(message)}\n\n";
            Assert.Equal(expected, writer1.GetWrittenContent());
            Assert.Equal(expected, writer2.GetWrittenContent());
        }

        [Fact]
        public async Task BroadcastMessageAsync_HandlesDisconnectedClients()
        {
            // Arrange
            var manager = new ClientManager();
            var writer1 = new TestWriter();
            var disconnectedWriter = new StreamWriter(new MemoryStream(new byte[0])); // This will cause write operations to fail
            manager.AddClient(writer1);
            manager.AddClient(disconnectedWriter);
            var message = new { test = "broadcast" };

            // Act
            await manager.BroadcastMessageAsync(message);

            // Assert
            Assert.Equal(1, manager.ClientCount); // The disconnected client should be removed
            var expected = $"data: {JsonSerializer.Serialize(message)}\n\n";
            Assert.Equal(expected, writer1.GetWrittenContent());
        }

        [Fact]
        public async Task SendMessageAsync_HandlesNonExistentClient()
        {
            // Arrange
            var manager = new ClientManager();
            var message = new { test = "message" };

            // Act & Assert
            // Should not throw exception for non-existent client
            await manager.SendMessageAsync(Guid.NewGuid(), message);
        }

        [Fact]
        public async Task SendMessageAsync_PreservesMessageContent()
        {
            // Arrange
            var manager = new ClientManager();
            var writer = new TestWriter();
            var id = manager.AddClient(writer);
            var message = new {
                number = 42,
                text = "test",
                nested = new { value = true }
            };

            // Act
            await manager.SendMessageAsync(id, message);

            // Assert
            var written = writer.GetWrittenContent();
            var expected = $"data: {JsonSerializer.Serialize(message)}\n\n";
            Assert.Equal(expected, written);
        }

        [Fact]
        public async Task ConcurrentOperations_HandleMultipleClients()
        {
            // Arrange
            var manager = new ClientManager();
            var writers = Enumerable.Range(0, 10)
                .Select(_ => new TestWriter())
                .ToList();
            var ids = writers.Select(w => manager.AddClient(w)).ToList();
            var message = new { test = "concurrent" };

            // Act
            var tasks = ids.Select(id => manager.SendMessageAsync(id, message));
            await Task.WhenAll(tasks);

            // Assert
            var expected = $"data: {JsonSerializer.Serialize(message)}\n\n";
            foreach (var writer in writers)
            {
                Assert.Equal(expected, writer.GetWrittenContent());
            }
        }
    }
}