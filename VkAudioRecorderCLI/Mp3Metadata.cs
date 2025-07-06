using TagLib;
using System.IO;
using System.Diagnostics;

public static class Mp3Metadata
{
    public static void WriteTags(string mp3Path, string title, string artist, byte[]? coverImageBytes = null)
    {
        var file = TagLib.File.Create(mp3Path);

        file.Tag.Title = title;
        file.Tag.Performers = new[] { artist };

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

        file.Save();
    }
}
