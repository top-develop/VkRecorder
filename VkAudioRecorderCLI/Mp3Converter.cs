using NAudio.Wave;
using NAudio.Lame;
using System;
using System.IO;

/// <summary>
/// Stellt Methoden zur Konvertierung von WAV-Audiodateien in das MP3-Format bereit.
/// Nutzt NAudio und Lame für die Umwandlung.
/// </summary>
public static class Mp3Converter
{
    /// <summary>
    /// Konvertiert eine WAV-Datei auf der Festplatte in eine MP3-Datei.
    /// </summary>
    /// <param name="wavPath">Pfad zur Eingabe-WAV-Datei.</param>
    /// <param name="mp3Path">Pfad zur Ausgabedatei (MP3).</param>
    public static void ConvertWavToMp3(string wavPath, string mp3Path)
    {
        // Liest die WAV-Datei und schreibt sie als MP3
        using var reader = new AudioFileReader(wavPath);
        using var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, 320);
        reader.CopyTo(writer);
    }

    /// <summary>
    /// Konvertiert einen WAV-Stream (z.B. aus dem Speicher) in eine MP3-Datei auf der Festplatte.
    /// </summary>
    /// <param name="wavStream">Eingabestream mit WAV-Daten (z.B. MemoryStream).</param>
    /// <param name="mp3Path">Pfad zur Ausgabedatei (MP3).</param>
    public static void ConvertWavToMp3(Stream wavStream, string mp3Path)
    {
        // Setzt die Position des Streams auf den Anfang
        wavStream.Position = 0;
        using var waveReader = new WaveFileReader(wavStream);
        using var mp3Writer = new LameMP3FileWriter(mp3Path, waveReader.WaveFormat, 320);
        waveReader.CopyTo(mp3Writer);
    }
}

