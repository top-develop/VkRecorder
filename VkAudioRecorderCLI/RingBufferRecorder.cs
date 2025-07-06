using System;
using System.IO;
using System.Timers;
using NAudio.Wave;
using Serilog;
using Timer = System.Timers.Timer;


namespace VkAudioRecorderCLI
{
    internal class RingBufferRecorder : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private WaveFormat? _waveFormat;
        private byte[]? _buffer;
        private int _bufferSize;
        private int _writePosition;
        private bool _bufferFilled;
        private string _title = "";
        private string _artist = "";
        private byte[]? _coverImageBytes = null;

        private readonly object _lock = new();

        private int _segmentStartPosition;
        private DateTime _segmentStartTime;

        private const int BufferSeconds = 60 * 10;
        private Timer? _ramCheckTimer;

        public RingBufferRecorder()
        {
            try
            {
                _capture = new WasapiLoopbackCapture();
                _waveFormat = _capture.WaveFormat;
                _bufferSize = _waveFormat.AverageBytesPerSecond * BufferSeconds;
                _buffer = new byte[_bufferSize];
                _writePosition = 0;
                _bufferFilled = false;

                _capture.DataAvailable += OnDataAvailable;

                if (!MemoryHelper.IsRamSufficient(out long availableMb))
                {
                    Log.Warning("Nicht genügend freier Arbeitsspeicher in Windows. Die Aufnahme startet nicht, um das System zu schützen. Verfügbar: {0} MB", availableMb);
                    return;
                }

                _capture.StartRecording();
                Log.Information("RingBufferRecorder gestartet. Puffergröße: {Size} Bytes", _bufferSize);

                _ramCheckTimer = new Timer(10000);
                _ramCheckTimer.Elapsed += CheckRamUsage;
                _ramCheckTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Initialisieren des RingBufferRecorders.");
                Dispose();
            }
        }

        private void CheckRamUsage(object? sender, ElapsedEventArgs e)
        {
            if (!MemoryHelper.IsRamSufficient(out long availableMb))
            {
                Log.Warning("⚠ RAM zu niedrig: Aufnahme wird gestoppt! Verfügbar: {0} MB", availableMb);
                Dispose();
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (_lock)
            {
                int bytesToWrite = e.BytesRecorded;
                int spaceAtEnd = _bufferSize - _writePosition;

                if (bytesToWrite <= spaceAtEnd)
                {
                    Array.Copy(e.Buffer, 0, _buffer!, _writePosition, bytesToWrite);
                    _writePosition += bytesToWrite;
                    if (_writePosition == _bufferSize)
                    {
                        _writePosition = 0;
                        _bufferFilled = true;
                    }
                }
                else
                {
                    Array.Copy(e.Buffer, 0, _buffer!, _writePosition, spaceAtEnd);
                    Array.Copy(e.Buffer, spaceAtEnd, _buffer!, 0, bytesToWrite - spaceAtEnd);
                    _writePosition = bytesToWrite - spaceAtEnd;
                    _bufferFilled = true;
                }
            }
        }

        public void ExportMarkedSegment(string filename)
        {
            lock (_lock)
            {
                if (_buffer == null || _waveFormat == null)
                {
                    Log.Warning("Kein Puffer vorhanden.");
                    return;
                }

                var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracks");
                Directory.CreateDirectory(outputDir);
                var fullPath = Path.Combine(outputDir, filename);

                if (File.Exists(fullPath))
                {
                    Log.Warning("Datei existiert bereits: {Filename}", fullPath);
                    return;
                }

                int start = _segmentStartPosition;
                int end = _writePosition;

                try
                {
                    using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                    using (var writer = new WaveFileWriter(fs, _waveFormat))
                    {
                        if (start <= end)
                        {
                            writer.Write(_buffer, start, end - start);
                        }
                        else
                        {
                            writer.Write(_buffer, start, _bufferSize - start);
                            writer.Write(_buffer, 0, end);
                        }
                    }

                    Log.Information("Segment exportiert: {Filename}", fullPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Segmentexport.");
                    return;
                }

                try
                {
                    var mp3Path = Path.ChangeExtension(fullPath, ".mp3");
                    if (!File.Exists(mp3Path))
                    {
                        Mp3Converter.ConvertWavToMp3(fullPath, mp3Path);
                        Mp3Metadata.WriteTags(mp3Path, _title, _artist, _coverImageBytes);
                        File.Delete(fullPath);
                        Log.Information("MP3 erzeugt: {Mp3File}", mp3Path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler bei der MP3-Konvertierung.");
                }
            }
        }

        public void MarkSegmentStart()
        {
            lock (_lock)
            {
                _segmentStartPosition = _writePosition;
                _segmentStartTime = DateTime.UtcNow;
            }
        }

        public void SetMetadata(string title, string artist, byte[]? coverImageBytes = null)
        {
            _title = title;
            _artist = artist;
            _coverImageBytes = coverImageBytes;
        }

        public void Dispose()
        {
            _ramCheckTimer?.Stop();
            _ramCheckTimer?.Dispose();
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
        }
    }
}
