using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DocShare.Helpers
{
    /// <summary>
    /// Manages file storage in a single flat directory.
    /// </summary>
    public class FileManager
    {
        private ILogger logger;
        private readonly DirectoryInfo root;

        /// <summary>
        /// Maximum time to keep files after upload. Defaults to 1 hour.
        /// </summary>
        public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(1);

        public FileManager(ILogger<FileManager> log, DirectoryInfo root)
        {
            this.logger = log;
            this.root = root;

            if (!root.Exists)
                root.Create();

            root.CreateSubdirectory("thumbnails");
        }

        /// <summary>
        /// Get a list of files.
        /// This will delete files older than MaxAge while scanning the directory.
        /// </summary>
        public IEnumerable<string> GetFileList()
        {
            var files = new List<string>();
            foreach (var f in root.GetFiles().OrderByDescending(f => f.CreationTime))
            {
                if (f.CreationTime + MaxAge < DateTime.Now)
                {
                    try
                    {
                        f.Delete();
                    }
                    catch (Exception ex)
                    {
                        // file may be locked or in use
                        logger.LogWarning(ex, "Failed to delete file {File}", f.Name);
                    }
                }
                else
                {
                    files.Add(f.Name);
                }
            }

            var thumb = root.GetDirectories("thumbnails").FirstOrDefault();
            if (thumb != null)
                foreach (var f in thumb.GetFiles().OrderByDescending(f => f.CreationTime))
                {
                    if (f.CreationTime + MaxAge < DateTime.Now)
                    {
                        try
                        {
                            f.Delete();
                        }
                        catch (Exception ex)
                        {
                            // file may be locked or in use
                            logger.LogWarning(ex, "Failed to delete file {File}", f.Name);
                        }
                    }
                }

            return files;
        }

        public async Task<string> AddFileAsync(string fileName, Stream fileStream)
        {
            fileName = SanitizeName(fileName);

            using (var f = File.Create(Path.Combine(root.FullName, fileName)))
            {
                await fileStream.CopyToAsync(f);
            }

            await GenerateThumbnail(fileName);

            return fileName;
        }

        public void DeleteFile(string fileName)
        {
            fileName = SanitizeName(fileName);

            var f = new FileInfo(Path.Combine(root.FullName, fileName));
            if (f.Exists)
                f.Delete();

            //var f = root.GetFiles(fileName).FirstOrDefault();
            //if (f == null)
            //    throw new Exception($"File {fileName} not found.");

            //f.Delete();
        }

        /// <summary>
        /// Sanitize filename to:
        /// Remove illegal windows file names
        /// Remove chars that need url encoding
        /// Avoid path traversal attacks
        /// </summary>
        private string SanitizeName(string fileName)
        {
            var name = Regex.Replace(fileName, @"[^\w\-.]", "-");
            name = Regex.Replace(name, @"-{2,}", "-");
            name = Regex.Replace(name, @"\.{2,}", ".");
            name = name.Replace("-.", ".");
            name = name.TrimEnd('.');
            name = name.Trim('-'); // cosmetic
            if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)", RegexOptions.IgnoreCase))
                name = "_" + name;
            if (name.Length > 100)
                name = name.Substring(0, 100);
            return name;
        }

        /// <summary>
        /// Generate a thumbnail using ffmpeg.
        /// The process start method will throw an exception if ffmpeg is not found in the path.
        /// </summary>
        private async Task GenerateThumbnail(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            var original = Path.Combine(root.FullName, fileName);
            var thumbnail = Path.Combine(root.FullName, "thumbnails", fileName + ".jpg");

            // video files, select a single frame 
            if (ext == ".mov" || ext == ".mp4" || ext == ".mkv")
            {
                var args = $"-y -i \"{original}\" -vf \"thumbnail,scale='if(gt(iw,ih),300,-1)':'if(gt(iw,ih),-1,200)'\" -vframes 1 \"{thumbnail}\"";
                await Execute("ffmpeg.exe", args);
            }

            // image files, resize to fit in 300x200
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                var args = $"-y -i \"{original}\" -vf \"scale='if(gt(iw,ih),300,-1)':'if(gt(iw,ih),-1,200)'\" \"{thumbnail}\"";
                await Execute("ffmpeg.exe", args);
            }
        }

        private async Task Execute(string path, string args)
        {
            var ps = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    WorkingDirectory = root.FullName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    //RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ErrorDialog = false,
                },
            };

            logger.LogInformation($"Running {path}: {args}");

            ps.OutputDataReceived += (sender, e) => logger.LogDebug(e.Data);
            ps.ErrorDataReceived += (sender, e) => logger.LogDebug(e.Data);
            ps.Start();
            //ps.StandardInput.Close();
            ps.BeginOutputReadLine();
            ps.BeginErrorReadLine();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ps.WaitForExitAsync(cts.Token);

            if (!ps.HasExited)
            {
                logger.LogWarning($"Aborted {path}");
                ps.CancelOutputRead();
                ps.CancelErrorRead();
            }
            else
            {
                logger.LogInformation($"Completed {path} with {ps.ExitCode}");
            }

        }
    }
}
