using NAudio.Wave;
using NAudio.Lame;
using System;
using System.IO;

public static class Mp3Converter
{
    public static void ConvertWavToMp3(string wavPath, string mp3Path)
    {
        using var reader = new AudioFileReader(wavPath);
        using var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, LAMEPreset.VBR_90);
        reader.CopyTo(writer);
    }

    public static void ConvertWavToMp3(Stream wavStream, string mp3Path)
    {
        wavStream.Position = 0;
        using var waveReader = new WaveFileReader(wavStream);
        using var mp3Writer = new LameMP3FileWriter(mp3Path, waveReader.WaveFormat, LAMEPreset.VBR_90);
        waveReader.CopyTo(mp3Writer);
    }
}
