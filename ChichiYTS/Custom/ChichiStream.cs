using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using ChichiYTS.Helpers;
using Newtonsoft.Json;

namespace ChichiYTS.Custom
{
    class ChichiStream : IRandomAccessStream
    {
        class MovieOutput
        {
            [JsonProperty("file_size")] public ulong FileSize { get; set; }
            [JsonProperty("piece_size")] public ulong PieceSize { get; set; }
            [JsonProperty("offset")] public ulong Offset { get; set; }
            [JsonProperty("mime_type")] public string MimeType { get; set; }
            [JsonProperty("download_rate")] public int DownloadRate { get; set; }
            [JsonProperty("pieces")] public List<int> Pieces { get; set; }
        }

        private readonly string _storagePath;
        private readonly string _movieOutputPath;
        private MovieOutput _movieOutput;
        private DateTime _modified;
        private bool _first;

        public ChichiStream(string storagePath)
        {
            _storagePath = storagePath;
            _movieOutputPath = $"{storagePath}\\{YtsServerHelper.MovieFolderName}\\movie_output.qza";

            while (true)
            {
                try
                {
                    _movieOutput = JsonConvert.DeserializeObject<MovieOutput>(File.ReadAllText(_movieOutputPath));
                    break;
                }
                catch (IOException) // accessing when writing, wait a moment
                {
                    Thread.Sleep(50);
                }
            }

            _first = true;
        }

        public string MimeType => _movieOutput.MimeType;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count,
            InputStreamOptions options)
        {
            return AsyncInfo.Run<IBuffer, uint>(async (token, progress) =>
            {
                try
                {
                    var oriCount = count;
                    var position = Position + _movieOutput.Offset;
                    var piece = -1;
                    var offset = -1;
                    var outStream = buffer.AsStream();

                    while (count > 0)
                    {
                        if (piece < 0)
                            piece = (int) (position / _movieOutput.PieceSize);

                        while (!_movieOutput.Pieces.Contains(piece))
                        {
                            var ok = await RefreshMovieOutputAsync();
                            if (!ok)
                                await Task.Delay(500, token);
                        }

                        var buffPath = $"{_storagePath}\\buffers\\{piece / 100}\\{piece}.buf";
                        using (var stream = File.OpenRead(buffPath))
                        {
                            try
                            {
                                if (offset < 0)
                                    offset = (int) (position % _movieOutput.PieceSize);

                                if (offset > 0)
                                    stream.Seek(offset, SeekOrigin.Begin);

                                var c = Math.Min((int) count, (int) _movieOutput.PieceSize - offset);
                                var buff = new byte[c];
                                var n = (uint) await stream.ReadAsync(buff, 0, c, token);
                                await outStream.WriteAsync(buff, 0, (int) n, token);
                                
                                if (n < count)
                                {
                                    // continue
                                    piece++;
                                    offset = 0;
                                }

                                count -= n;
                            }
                            catch (Exception e)
                            {
                                var s = e.Message;
                            }
                        }
                    }

                    buffer.Length = oriCount;
                    progress.Report(oriCount);
                    Position += oriCount;
                }
                catch (Exception e)
                {
                    throw;
                }

                return buffer;
            });
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            throw new NotImplementedException();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public void Seek(ulong position)
        {
            if (_first || position != Position)
            {
                YtsServerHelper.SetPosition(position);
                Position = position;
                _first = false;
            }
        }

        public IRandomAccessStream CloneStream()
        {
            return this;
        }

        public bool CanRead => true;

        public bool CanWrite => false;

        public ulong Position { get; private set; }

        public ulong Size
        {
            get => _movieOutput.FileSize;
            set { }
        }

        public int GetDownloadRate()
        {
            return _movieOutput.DownloadRate;
        }

        private async Task<bool> RefreshMovieOutputAsync()
        {
            var modified = File.GetLastWriteTimeUtc(_movieOutputPath);
            if (modified == _modified)
                return false;

            _modified = modified;
            while (true)
            {
                try
                {
                    _movieOutput = JsonConvert.DeserializeObject<MovieOutput>(await File.ReadAllTextAsync(_movieOutputPath));
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
            }

            return true;
        }
    }
}
