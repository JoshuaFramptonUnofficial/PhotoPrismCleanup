using System;
using System.Collections.Generic;
using System.IO;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace PhotoPrismCleanup
{
    public class PhotoPrismService : IDisposable
    {
        private readonly string _remoteFolder;
        private readonly string _thumbCacheFolder;
        private SftpClient _client;

        public static readonly string[] ImageExts = {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".heic"
        };
        public static readonly string[] VideoExts = {
            ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm"
        };

        public PhotoPrismService(string remoteFolder, string thumbCacheFolder)
        {
            _remoteFolder = remoteFolder;
            _thumbCacheFolder = thumbCacheFolder;
        }

        public void Connect(string host, int port, string user, string pwdOrKey, bool useKey, string keyPath)
        {
            if (_client?.IsConnected == true) Dispose();

            AuthenticationMethod auth = useKey
                ? new PrivateKeyAuthenticationMethod(user,
                      string.IsNullOrEmpty(pwdOrKey)
                        ? new PrivateKeyFile(keyPath)
                        : new PrivateKeyFile(keyPath, pwdOrKey))
                : new PasswordAuthenticationMethod(user, pwdOrKey);

            var conn = new ConnectionInfo(host, port, user, auth);
            _client = new SftpClient(conn);
            _client.Connect();
            _client.KeepAliveInterval = TimeSpan.FromMinutes(1);
        }

        public List<string> ListAllMedia()
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("Not connected.");
            var files = new List<string>();
            Recurse(_remoteFolder, files);
            return files;
        }

        private void Recurse(string path, List<string> outList)
        {
            foreach (var entry in _client.ListDirectory(path))
            {
                // skip current and parent dirs
                if (entry.Name == "." || entry.Name == "..") continue;
                if (entry.IsDirectory)
                {
                    Recurse(entry.FullName, outList);
                }
                else
                {
                    string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (Array.Exists(ImageExts, e => e == ext) ||
                        Array.Exists(VideoExts, e => e == ext))
                    {
                        outList.Add(entry.FullName);
                    }
                }
            }
        }

        public byte[] DownloadToMemory(string remotePath)
        {
            using var ms = new MemoryStream();
            _client.DownloadFile(remotePath, ms);
            return ms.ToArray();
        }

        public void DownloadToFile(string remotePath, string localPath)
        {
            using var fs = File.OpenWrite(localPath);
            _client.DownloadFile(remotePath, fs);
        }

        public List<string> DeleteFiles(IEnumerable<string> paths)
        {
            var failed = new List<string>();
            foreach (var p in paths)
            {
                try { _client.DeleteFile(p); }
                catch { failed.Add(p); }
            }
            return failed;
        }

        public void ClearThumbnailCache()
        {
            // same skip-“..” fix here
            RecurseDelete(_thumbCacheFolder);
        }
        private void RecurseDelete(string path)
        {
            foreach (var entry in _client.ListDirectory(path))
            {
                if (entry.Name == "." || entry.Name == "..") continue;
                if (entry.IsDirectory)
                {
                    RecurseDelete(entry.FullName);
                    _client.DeleteDirectory(entry.FullName);
                }
                else
                {
                    _client.DeleteFile(entry.FullName);
                }
            }
        }

        public void ImportFiles(IEnumerable<string> localPaths)
        {
            foreach (var local in localPaths)
            {
                string fileName = Path.GetFileName(local);
                string remote = _remoteFolder.TrimEnd('/') + "/" + fileName;
                using var fs = File.OpenRead(local);
                _client.UploadFile(fs, remote);
            }
        }

        public void Disconnect()
        {
            if (_client != null)
            {
                if (_client.IsConnected) _client.Disconnect();
                _client.Dispose();
                _client = null;
            }
        }

        public void Dispose() => Disconnect();
    }
}
