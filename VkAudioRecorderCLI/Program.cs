
using System;
using System.IO;
using System.Threading;
using Serilog;

namespace VkAudioRecorderCLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(@"Logs\log.txt", encoding: System.Text.Encoding.UTF8, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Anwendung gestartet.");
            VkMetadataFetcher.StartBrowser();

            using var ringBufferRecorder = new RingBufferRecorder();

            string lastTrackTime = "";
            string artist = "Artist";
            string title = "Title";
            string lastArtist = "";
            string lastTitle = "";
            int unchangedTimeCount = 0;
            int millisecondsSinceStart = 0;
            int currentTrackDuration = 0;
            bool waitingForTrackStart = false;
            bool trackJustStarted = false;
            DateTime trackStartTime = DateTime.UtcNow;

            while (true)
            {
                string trackTime = VkMetadataFetcher.GetTrackTime();
                artist = VkMetadataFetcher.GetArtist();
                title = VkMetadataFetcher.GetTitle();
                currentTrackDuration += 100;

                if (millisecondsSinceStart % 1000 == 0)
                {
                    Log.Information("Metadaten prüfen... Zeit: {TrackTime}, Artist: {Artist}, Titel: {Title}", trackTime, artist, title);
                }

                if (!waitingForTrackStart)
                {
                    if (artist != lastArtist || title != lastTitle)
                    {
                        millisecondsSinceStart = 0;
                        unchangedTimeCount = 0;

                        var duration = (DateTime.UtcNow - trackStartTime).TotalMilliseconds;
                        Log.Information("Trackdauer (ms): {Duration}", duration);

                        string safeArtist = MakeFilenameSafe(lastArtist);
                        string safeTitle = MakeFilenameSafe(lastTitle);
                        string filename = $"{safeArtist}_{safeTitle}.wav";
                        string mp3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracks", Path.ChangeExtension(filename, ".mp3"));

                        if (File.Exists(mp3Path))
                        {
                            Log.Information("MP3 existiert bereits: {Mp3File}. Export wird komplett übersprungen.", mp3Path);
                        }
                        else if (duration >= 15000)
                        {
                            ringBufferRecorder.ExportMarkedSegment(filename);
                            Log.Information("⏹️ Aufnahme gestoppt, WAV-Datei gespeichert: {Filename}", filename);
                            Log.Information("Trackwechsel erkannt: {LastArtist} - {LastTitle} gespeichert als {Filename}", lastArtist, lastTitle, filename);
                            File.AppendAllText("recorded.txt", $"{lastArtist} - {lastTitle}\n");
                        }
                        else
                        {
                            Log.Information("Trackwechsel erkannt: {LastArtist} - {LastTitle} war zu kurz, wird nicht gespeichert.", lastArtist, lastTitle);
                        }

                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                    }

                    if (trackTime == "0:00" && lastTrackTime != "0:00")
                    {
                        string safeArtist = MakeFilenameSafe(artist);
                        string safeTitle = MakeFilenameSafe(title);
                        string filename = $"{safeArtist}_{safeTitle}.wav";
                        string mp3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracks", Path.ChangeExtension(filename, ".mp3"));

                        if (File.Exists(mp3Path))
                        {
                            Log.Information("Trackstart erkannt, aber MP3 existiert bereits: {Mp3File}. Aufnahme wird übersprungen.", mp3Path);
                            waitingForTrackStart = true;
                            continue;
                        }

                        trackJustStarted = true;
                        millisecondsSinceStart = 0;
                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                        Log.Information("Trackstart erkannt → Zähler auf 0.");

                        ringBufferRecorder.SetMetadata(title, artist);
                        ringBufferRecorder.MarkSegmentStart();
                        Log.Information("🎙️ Aufnahme gestartet (in Memory) für: {Artist} - {Title}", artist, title);

                    }
                    else
                    {
                        millisecondsSinceStart += 100;
                        if (trackJustStarted) trackJustStarted = false;
                    }

                    if (trackTime == lastTrackTime)
                    {
                        unchangedTimeCount++;
                        if (unchangedTimeCount >= 30)
                        {
                            Log.Information("Track zu Ende");
                            waitingForTrackStart = true;

                            var duration = (DateTime.UtcNow - trackStartTime).TotalMilliseconds;
                            Log.Information("Trackdauer (ms): {Duration}", duration);

                            string safeArtist = MakeFilenameSafe(artist);
                            string safeTitle = MakeFilenameSafe(title);
                            string filename = $"{safeArtist}_{safeTitle}.wav";
                            string mp3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracks", Path.ChangeExtension(filename, ".mp3"));

                            if (File.Exists(mp3Path))
                            {
                                Log.Information("MP3 existiert bereits: {Mp3File}. Export wird komplett übersprungen.", mp3Path);
                            }
                            else if (duration >= 15000)
                            {
                                ringBufferRecorder.ExportMarkedSegment(filename);
                                Log.Information("⏹️ Aufnahme gestoppt, WAV-Datei gespeichert: {Filename}", filename);
                                Log.Information("Track war lang genug, Segment exportiert: {Filename}", filename);
                                File.AppendAllText("recorded.txt", $"{artist} - {title}\n");
                            }
                            else
                            {
                                Log.Information("Track war zu kurz, wird nicht gespeichert.");
                            }
                        }
                    }
                    else
                    {
                        unchangedTimeCount = 0;
                    }
                }
                else
                {
                    if (trackTime == "0:00" && lastTrackTime != "0:00")
                    {
                        waitingForTrackStart = false;
                        unchangedTimeCount = 0;
                        millisecondsSinceStart = 0;
                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                        Log.Information("Trackstart erkannt, Segmentmarkierung wurde bereits gesetzt.");

                        Log.Information("Trackstart: Artist = {Artist}, Title = {Title}", artist, title);
                        ringBufferRecorder.SetMetadata(title, artist);
                        ringBufferRecorder.MarkSegmentStart();
                        Log.Information("🎙️ Aufnahme gestartet (in Memory) für: {Artist} - {Title}", artist, title);

                    }
                }

                lastTrackTime = trackTime;
                lastArtist = artist;
                lastTitle = title;
                Thread.Sleep(100);
            }
        }

        private static string MakeFilenameSafe(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }
            return input;
        }
    }
}
