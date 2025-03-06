using DocShare.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocShareTests
{
    public class FileManagerTests : IDisposable
    {
        private readonly DirectoryInfo tempFolder;
        private readonly ILogger<FileManager> logger;
        private readonly FileManager fileStorage;

        public FileManagerTests()
        {
            // Setup test directory
            tempFolder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "DocShareTests_" + Guid.NewGuid()));
            logger = NullLogger<FileManager>.Instance;
            fileStorage = new FileManager(logger, tempFolder);
        }

        public void Dispose()
        {
            // Cleanup after tests
            if (tempFolder.Exists)
            {
                tempFolder.Delete(true);
            }
        }

        [Fact]
        public void Constructor_CreatesDirectoryAndThumbnailsFolder()
        {
            // Assert
            Assert.True(tempFolder.Exists);
            Assert.True(tempFolder.GetDirectories("thumbnails").Any());
        }

        [Theory]
        [InlineData("test.txt", "test.txt")]
        [InlineData("test file.txt", "test-file.txt")]
        [InlineData("../test.txt", "test.txt")]
        [InlineData("test..txt", "test.txt")]
        [InlineData("test....txt", "test.txt")]
        [InlineData("test@#$%^&*.txt", "test.txt")]
        [InlineData("prn", "_prn")]
        [InlineData("prn.txt", "_prn.txt")]
        public async Task AddFileAsync_SanitizesFileName(string inputName, string expectedName)
        {
            // Arrange
            var content = "test content";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

            // Act
            var result = await fileStorage.AddFileAsync(inputName, stream);

            // Assert
            Assert.Equal(expectedName, result);
            Assert.True(File.Exists(Path.Combine(tempFolder.FullName, expectedName)));
        }

        [Fact]
        public void GetFileList_ReturnsFilesAndDeletesExpired()
        {
            // Arrange
            fileStorage.MaxAge = TimeSpan.FromMinutes(5);

            // Create a file that should be expired
            var oldFile = Path.Combine(tempFolder.FullName, "old.txt");
            File.WriteAllText(oldFile, "old content");
            File.SetCreationTime(oldFile, DateTime.Now.AddHours(-1));

            // Create a current file
            var newFile = Path.Combine(tempFolder.FullName, "new.txt");
            File.WriteAllText(newFile, "new content");

            // Act
            var files = fileStorage.GetFileList().ToList();

            // Assert
            Assert.Single(files);
            Assert.Contains("new.txt", files);
            Assert.DoesNotContain("old.txt", files);
        }

        [Fact]
        public void DeleteFile_RemovesExistingFile()
        {
            // Arrange
            var fileName = "test.txt";
            var filePath = Path.Combine(tempFolder.FullName, fileName);
            File.WriteAllText(filePath, "test content");

            // Act
            fileStorage.DeleteFile(fileName);

            // Assert
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public void DeleteFile_DoesNotThrowForNonExistentFile()
        {
            // Act & Assert
            var exception = Record.Exception(() => fileStorage.DeleteFile("nonexistent.txt"));
            Assert.Null(exception);
        }

        [Fact]
        public async Task AddFileAsync_CreatesFileWithCorrectContent()
        {
            // Arrange
            var fileName = "test.txt";
            var content = "test content";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

            // Act
            var result = await fileStorage.AddFileAsync(fileName, stream);

            // Assert
            var filePath = Path.Combine(tempFolder.FullName, result);
            Assert.True(File.Exists(filePath));
            Assert.Equal(content, await File.ReadAllTextAsync(filePath));
        }
    }
}