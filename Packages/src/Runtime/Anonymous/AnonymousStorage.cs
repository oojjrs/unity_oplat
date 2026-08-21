using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousStorage : MyStorageServiceInterface
    {
        private const int FileBufferByteCount = 81920;
        private const string FilesDirectoryName = "files";
        private const int ProjectKeyByteCountMax = 1024;
        private const string TemporaryDirectoryName = "temp";

        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly HashSet<string> ReadingFileNames = new(StringComparer.Ordinal);

        private string _accountRootPath;
        private bool _isInitialized;
        private string _storageBasePath;

        int MyStorageServiceInterface.FileByteCountMax => MyStorage.FileByteCountMax;

        async Task<bool> MyStorageServiceInterface.DeleteAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                return await Task.Run(() => Delete(fileName, cancellationToken), cancellationToken);
            }
        }

        async Task<bool> MyStorageServiceInterface.ExistsAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                return await Task.Run(() => Exists(fileName, cancellationToken), cancellationToken);
            }
        }

        async Task<IReadOnlyList<MyStorageServiceInterface.FileInfo>> MyStorageServiceInterface.ListAsync(CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                return await Task.Run(() => GetFiles(cancellationToken), cancellationToken);
            }
        }

        async Task<MyStorageServiceInterface.ReadResult> MyStorageServiceInterface.ReadAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadingFileNames.Add(fileName) == false)
                    throw new InvalidOperationException($"An anonymous storage read is already in progress for '{fileName}'.");

                try
                {
                    return await Task.Run(() => ReadAsync(fileName, cancellationToken), cancellationToken);
                }
                finally
                {
                    ReadingFileNames.Remove(fileName);
                }
            }
        }

        async Task MyStorageServiceInterface.WriteAsync(string fileName, byte[] data, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            MyStorage.EnsureData(data);
            callerCancellationToken.ThrowIfCancellationRequested();
            var snapshot = new byte[data.Length];
            Buffer.BlockCopy(data, 0, snapshot, 0, data.Length);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                await Task.Run(() => WriteAsync(fileName, snapshot, cancellationToken), cancellationToken);
            }
        }

        internal void Initialize(uint appId, string projectKey, string account)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
                throw new ArgumentException("An anonymous storage project key is required.", nameof(projectKey));

            if (Encoding.UTF8.GetByteCount(projectKey) > ProjectKeyByteCountMax)
                throw new ArgumentException($"An anonymous storage project key cannot exceed {ProjectKeyByteCountMax} UTF-8 bytes.", nameof(projectKey));

            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("An anonymous storage account is required.", nameof(account));

            if (LifetimeCancellationSource.IsCancellationRequested)
                throw new InvalidOperationException("Anonymous storage has been shut down.");

            var storageBasePath = GetStorageBasePath();
            var accountRootPath = GetAccountRootPath(storageBasePath, appId, projectKey, account);
            if (_isInitialized)
            {
                if (string.Equals(_accountRootPath, accountRootPath, StringComparison.Ordinal) == false)
                    throw new InvalidOperationException("Anonymous storage is already initialized for another project or account.");

                return;
            }

            _accountRootPath = accountRootPath;
            _storageBasePath = storageBasePath;
            _isInitialized = true;
        }

        internal void Shutdown()
        {
            _isInitialized = false;
            LifetimeCancellationSource.Cancel();
        }

        private CancellationTokenSource CreateCancellationSource(CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            return CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, LifetimeCancellationSource.Token);
        }

        private bool Delete(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = GetTargetPath(fileName);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(targetPath);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }

            if ((attributes & FileAttributes.Directory) != 0)
                return false;

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The anonymous storage target cannot be a reparse point.");

            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(targetPath);
            return true;
        }

        private void EnsureInitialized()
        {
            if (_isInitialized == false || LifetimeCancellationSource.IsCancellationRequested)
                throw new InvalidOperationException("Anonymous storage is not initialized.");
        }

        private bool Exists(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = GetTargetPath(fileName);
            try
            {
                var attributes = File.GetAttributes(targetPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The anonymous storage target cannot be a reparse point.");

                return (attributes & FileAttributes.Directory) == 0;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static string GetAccountRootPath(string storageBasePath, uint appId, string projectKey, string account)
        {
            return Path.GetFullPath(Path.Combine(storageBasePath, "oojjrs", "Oplat", "AnonymousStorage", "v1", GetStorageKeyHash(projectKey), appId.ToString(CultureInfo.InvariantCulture), "users", GetStorageKeyHash(account)));
        }

        private static string GetStorageBasePath()
        {
            var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationDataPath))
                throw new InvalidOperationException("The local application data path is unavailable.");

            return Path.GetFullPath(localApplicationDataPath);
        }

        private IReadOnlyList<MyStorageServiceInterface.FileInfo> GetFiles(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filesRootPath = GetFilesRootPath();
            FileAttributes rootAttributes;
            try
            {
                rootAttributes = File.GetAttributes(filesRootPath);
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<MyStorageServiceInterface.FileInfo>();
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<MyStorageServiceInterface.FileInfo>();
            }

            if ((rootAttributes & FileAttributes.Directory) == 0)
                throw new IOException("The anonymous storage files root is not a directory.");

            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The anonymous storage root cannot be a reparse point.");

            EnsureDirectoryPathIsSafe(filesRootPath);

            var files = new List<MyStorageServiceInterface.FileInfo>();
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(filesRootPath);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directoryPath = pendingDirectories.Pop();
                foreach (var filePath in Directory.EnumerateFiles(directoryPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(filePath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    var file = new System.IO.FileInfo(filePath);
                    if (file.Exists == false)
                        continue;

                    var logicalFileName = GetLogicalFileName(filesRootPath, file.FullName);
                    MyStorage.EnsureFileName(logicalFileName);
                    files.Add(new MyStorageServiceInterface.FileInfo(logicalFileName, DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc), file.Length));
                }

                foreach (var childDirectoryPath in Directory.EnumerateDirectories(directoryPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(childDirectoryPath) & FileAttributes.ReparsePoint) == 0)
                        pendingDirectories.Push(childDirectoryPath);
                }
            }

            files.Sort((left, right) => StringComparer.Ordinal.Compare(left.FileName, right.FileName));
            return files.ToArray();
        }

        private string GetFilesRootPath()
        {
            return Path.Combine(_accountRootPath, FilesDirectoryName);
        }

        private void EnsureDirectoryPathIsSafe(string directoryPath)
        {
            var basePath = Path.GetFullPath(_storageBasePath);
            var targetPath = Path.GetFullPath(directoryPath);
            var basePrefix = basePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? basePath : basePath + Path.DirectorySeparatorChar;
            if (targetPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase) == false)
                throw new IOException("An anonymous storage directory escaped its storage base path.");

            var relativePath = targetPath[basePrefix.Length..];
            var currentPath = basePath;
            foreach (var segment in relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(currentPath);
                }
                catch (FileNotFoundException)
                {
                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }

                if ((attributes & FileAttributes.Directory) == 0)
                    throw new IOException("An anonymous storage path component is not a directory.");

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("An anonymous storage path component cannot be a reparse point.");
            }
        }

        private static string GetLogicalFileName(string filesRootPath, string filePath)
        {
            var rootPrefix = filesRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? filesRootPath : filesRootPath + Path.DirectorySeparatorChar;
            if (filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) == false)
                throw new IOException("An anonymous storage file escaped its storage root.");

            return filePath[rootPrefix.Length..].Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string GetStorageKeyHash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(hash.Length * 2);
                foreach (var item in hash)
                    result.Append(item.ToString("x2", CultureInfo.InvariantCulture));

                return result.ToString();
            }
        }

        private string GetTargetPath(string fileName)
        {
            var filesRootPath = Path.GetFullPath(GetFilesRootPath());
            var targetPath = filesRootPath;
            foreach (var segment in fileName.Split('/'))
                targetPath = Path.Combine(targetPath, segment);

            targetPath = Path.GetFullPath(targetPath);
            var rootPrefix = filesRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? filesRootPath : filesRootPath + Path.DirectorySeparatorChar;
            if (targetPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) == false)
                throw new ArgumentException("An anonymous storage file escaped its storage root.", nameof(fileName));

            EnsureDirectoryPathIsSafe(Path.GetDirectoryName(targetPath));
            return targetPath;
        }

        private async Task<MyStorageServiceInterface.ReadResult> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            var targetPath = GetTargetPath(fileName);
            try
            {
                var attributes = File.GetAttributes(targetPath);
                if ((attributes & FileAttributes.Directory) != 0)
                    return new MyStorageServiceInterface.ReadResult(false, Array.Empty<byte>());

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The anonymous storage target cannot be a reparse point.");

                using (var stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, FileBufferByteCount, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    if (stream.Length > MyStorage.FileByteCountMax)
                        throw new InvalidDataException($"Anonymous storage files cannot exceed {MyStorage.FileByteCountMax} bytes.");

                    var data = new byte[checked((int)stream.Length)];
                    var offset = 0;
                    while (offset < data.Length)
                    {
                        var readByteCount = await stream.ReadAsync(data, offset, data.Length - offset, cancellationToken);
                        if (readByteCount == 0)
                            throw new EndOfStreamException("The anonymous storage file ended before it was fully read.");

                        offset += readByteCount;
                    }

                    return new MyStorageServiceInterface.ReadResult(true, data);
                }
            }
            catch (FileNotFoundException)
            {
                return new MyStorageServiceInterface.ReadResult(false, Array.Empty<byte>());
            }
            catch (DirectoryNotFoundException)
            {
                return new MyStorageServiceInterface.ReadResult(false, Array.Empty<byte>());
            }
        }

        private async Task WriteAsync(string fileName, byte[] data, CancellationToken cancellationToken)
        {
            var targetPath = GetTargetPath(fileName);
            var targetDirectoryPath = Path.GetDirectoryName(targetPath);
            var temporaryDirectoryPath = Path.Combine(_accountRootPath, TemporaryDirectoryName);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(targetDirectoryPath);
            Directory.CreateDirectory(temporaryDirectoryPath);
            EnsureDirectoryPathIsSafe(targetDirectoryPath);
            EnsureDirectoryPathIsSafe(temporaryDirectoryPath);

            var temporaryPath = Path.Combine(temporaryDirectoryPath, $"{Guid.NewGuid():N}.tmp");
            var isCommitted = false;
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileBufferByteCount, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(targetPath))
                    throw new IOException("The anonymous storage target is a directory.");

                if (File.Exists(targetPath))
                {
                    if ((File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("The anonymous storage target cannot be a reparse point.");

                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }

                isCommitted = true;
            }
            finally
            {
                if (isCommitted == false)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }
}
