using TagLib;
using System.IO;
using System.Diagnostics;

/// <summary>
/// Stellt Hilfsmethoden zum Schreiben von Metadaten (Tags) in MP3-Dateien bereit.
/// Nutzt die TagLib-Bibliothek für das Setzen von Titel, Künstler und optionalem Cover-Bild.
/// </summary>
public static class Mp3Metadata
{
    /// <summary>
    /// Schreibt Titel, Künstler und optional ein Cover-Bild in die Metadaten einer MP3-Datei.
    /// </summary>
    /// <param name="mp3Path">Pfad zur MP3-Datei, die getaggt werden soll.</param>
    /// <param name="title">Titel des Tracks.</param>
    /// <param name="artist">Künstler des Tracks.</param>
    /// <param name="coverImageBytes">Optional: Byte-Array eines Cover-Bildes (JPEG oder PNG).</param>
    public static void WriteTags(string mp3Path, string title, string artist, byte[]? coverImageBytes = null)
    {
        // Öffnet die MP3-Datei mit TagLib
        var file = TagLib.File.Create(mp3Path);

        // Setzt Titel und Künstler
        file.Tag.Title = title;
        file.Tag.Performers = new[] { artist };

        // Optional: Cover-Bild hinzufügen, falls vorhanden
        if (coverImageBytes != null)
        {
            var picture = new Picture
            {
                Type = PictureType.FrontCover,
                Description = "Cover",
                MimeType = "image/jpeg", // oder "image/png"
                Data = coverImageBytes
            };

            file.Tag.Pictures = new IPicture[] { picture };
        }

        // Speichert die Änderungen in der MP3-Datei
        file.Save();
    }
}

