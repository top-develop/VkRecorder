using System;
using System.IO;
using System.Timers;
using NAudio.Lame;
using NAudio.Wave;
using Serilog;
using Timer = System.Timers.Timer;

namespace VkAudioRecorderCLI
{
    /// <summary>
    /// Ringpuffer-basierter Audio-Recorder, der kontinuierlich den System-Sound aufnimmt.
    /// Unterstützt Segmentierung, RAM-Überwachung und Export als WAV/MP3.
    /// </summary>
    internal class RingBufferRecorder : IDisposable
    {
        /// <summary>
        /// NAudio-Komponente für das Loopback-Capturing (System-Sound).
        /// </summary>
        private WasapiLoopbackCapture? _capture;

        /// <summary>
        /// Audioformat der Aufnahme.
        /// </summary>
        private WaveFormat? _waveFormat;

        /// <summary>
        /// Der eigentliche Ringpuffer für die Audiodaten.
        /// </summary>
        private byte[]? _buffer;

        /// <summary>
        /// Größe des Puffers in Bytes.
        /// </summary>
        private int _bufferSize;

        /// <summary>
        /// Aktuelle Schreibposition im Puffer.
        /// </summary>
        private int _writePosition;

        /// <summary>
        /// Gibt an, ob der Puffer mindestens einmal komplett gefüllt wurde.
        /// </summary>
        private bool _bufferFilled;

        /// <summary>
        /// Metadaten: Titel des aktuellen Tracks.
        /// </summary>
        private string _title = "";

        /// <summary>
        /// Metadaten: Künstler des aktuellen Tracks.
        /// </summary>
        private string _artist = "";

        /// <summary>
        /// Optionales Cover-Bild für MP3-Tags.
        /// </summary>
        private byte[]? _coverImageBytes = null;

        /// <summary>
        /// Synchronisationsobjekt für Thread-Sicherheit.
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// Startposition des aktuellen Segments im Puffer.
        /// </summary>
        private int _segmentStartPosition;

        /// <summary>
        /// Startzeitpunkt des aktuellen Segments.
        /// </summary>
        private DateTime _segmentStartTime;

        /// <summary>
        /// Maximale Pufferlänge in Sekunden (z.B. 10 Minuten).
        /// </summary>
        private const int BufferSeconds = 60 * 10;

        /// <summary>
        /// Timer zur regelmäßigen Überwachung des freien Arbeitsspeichers.
        /// </summary>
        private Timer? _ramCheckTimer;

        /// <summary>
        /// Initialisiert den Recorder, prüft RAM, startet Aufnahme und RAM-Überwachung.
        /// </summary>
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

                // RAM-Check vor Start
                if (!MemoryHelper.IsRamSufficient(out long availableMb))
                {
                    Log.Warning("Nicht genügend freier Arbeitsspeicher in Windows. Die Aufnahme startet nicht, um das System zu schützen. Verfügbar: {0} MB", availableMb);
                    return;
                }

                _capture.StartRecording();
                Log.Information("RingBufferRecorder gestartet. Puffergröße: {Size} Bytes", _bufferSize);

                // RAM-Überwachung alle 10 Sekunden
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

        /// <summary>
        /// Prüft regelmäßig, ob noch genügend RAM verfügbar ist. Stoppt die Aufnahme bei zu wenig RAM.
        /// </summary>
        private void CheckRamUsage(object? sender, ElapsedEventArgs e)
        {
            if (!MemoryHelper.IsRamSufficient(out long availableMb))
            {
                Log.Warning("⚠ RAM zu niedrig: Aufnahme wird gestoppt! Verfügbar: {0} MB", availableMb);
                Dispose();
            }
        }

        /// <summary>
        /// Wird bei neuen Audiodaten aufgerufen und schreibt diese in den Ringpuffer.
        /// </summary>
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
                    // Split-Write: erst bis zum Ende, dann von Anfang
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

                var outputDir = Environment.ExpandEnvironmentVariables(ConfigHelper.Get("Paths:TracksDirectory"));
                Directory.CreateDirectory(outputDir);

                var mp3Path = Path.Combine(outputDir, filename);

                if (File.Exists(mp3Path))
                {
                    Log.Warning("MP3-Datei existiert bereits: {Filename}", mp3Path);
                    return;
                }

                int start = _segmentStartPosition;
                int end = _writePosition;

                try
                {
                    using var fs = new FileStream(mp3Path, FileMode.Create, FileAccess.Write);
                    using var writer = new LameMP3FileWriter(fs, _waveFormat, LAMEPreset.VBR_90);

                    if (start <= end)
                    {
                        writer.Write(_buffer, start, end - start);
                    }
                    else
                    {
                        writer.Write(_buffer, start, _bufferSize - start);
                        writer.Write(_buffer, 0, end);
                    }

                    Log.Information("🎧 MP3-Datei erstellt: {Mp3Path}", mp3Path);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Schreiben der MP3-Datei.");
                    return;
                }

                try
                {
                    Mp3Metadata.WriteTags(mp3Path, _title, _artist, _coverImageBytes);
                    Log.Information("ID3-Tags gesetzt für: {Mp3Path}", mp3Path);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Schreiben der MP3-Tags.");
                }
            }
        }


        /// <summary>
        /// Markiert den aktuellen Pufferstand als Start eines neuen Segments.
        /// </summary>
        public void MarkSegmentStart()
        {
            lock (_lock)
            {
                _segmentStartPosition = _writePosition;
                _segmentStartTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Setzt die Metadaten für das nächste zu exportierende Segment.
        /// </summary>
        public void SetMetadata(string title, string artist, byte[]? coverImageBytes = null)
        {
            _title = title;
            _artist = artist;
            _coverImageBytes = coverImageBytes;
        }

        /// <summary>
        /// Beendet die Aufnahme, stoppt den Timer und gibt Ressourcen frei.
        /// </summary>
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

