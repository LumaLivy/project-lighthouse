using System;
using System.IO;
using System.Linq;
using System.Text;
using LBPUnion.ProjectLighthouse.Types.Files;
using LBPUnion.ProjectLighthouse.Types.Settings;

namespace LBPUnion.ProjectLighthouse.Helpers
{
    public static class FileHelper
    {
        public static readonly string ResourcePath = Path.Combine(Environment.CurrentDirectory, "r");

        public static string GetResourcePath(string hash) => Path.Combine(ResourcePath, hash);

        public static bool IsFileSafe(LbpFile file)
        {
            if (!ServerSettings.Instance.CheckForUnsafeFiles) return true;

            if (file.FileType == LbpFileType.Unknown) file.FileType = DetermineFileType(file.Data);

            return file.FileType switch
            {
                LbpFileType.FileArchive => false,
                LbpFileType.Painting => true,
                LbpFileType.Unknown => false,
                LbpFileType.Texture => true,
                LbpFileType.Script => false,
                LbpFileType.Level => true,
                LbpFileType.Voice => true,
                LbpFileType.Plan => true,
                LbpFileType.Jpeg => true,
                LbpFileType.Png => true,
                #if DEBUG
                _ => throw new ArgumentOutOfRangeException(nameof(file), $"Unhandled file type ({file.FileType}) in FileHelper.IsFileSafe()"),
                #else
                _ => false,
                #endif
            };
        }

        public static LbpFileType DetermineFileType(byte[] data)
        {
            if (data.Length == 0) return LbpFileType.Unknown; // Can't be anything if theres no data.

            using MemoryStream ms = new(data);
            using BinaryReader reader = new(ms);

            // Determine if file is a FARC (File Archive).
            // Needs to be done before anything else that determines the type by the header
            // because this determines the type by the footer.
            string footer = Encoding.ASCII.GetString(BinaryHelper.ReadLastBytes(reader, 4));
            if (footer == "FARC") return LbpFileType.FileArchive;

            byte[] header = reader.ReadBytes(3);

            return Encoding.ASCII.GetString(header) switch
            {
                "PTG" => LbpFileType.Painting,
                "TEX" => LbpFileType.Texture,
                "FSH" => LbpFileType.Script,
                "VOP" => LbpFileType.Voice,
                "LVL" => LbpFileType.Level,
                "PLN" => LbpFileType.Plan,
                _ => determineFileTypePartTwoWeirdName(reader),
            };
        }

        private static LbpFileType determineFileTypePartTwoWeirdName(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;

            // Determine if file is JPEG/PNG
            byte[] header = reader.ReadBytes(9);

            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF && header[3] == 0xE0) return LbpFileType.Jpeg;
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return LbpFileType.Png;

            return LbpFileType.Unknown; // Still unknown.
        }

        public static bool ResourceExists(string hash) => File.Exists(GetResourcePath(hash));

        public static int ResourceSize(string hash)
        {
            try
            {
                return (int)new FileInfo(GetResourcePath(hash)).Length;
            }
            catch
            {
                return 0;
            }
        }

        public static void EnsureDirectoryCreated(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path ?? throw new ArgumentNullException(nameof(path)));
        }

        public static string[] ResourcesNotUploaded(params string[] hashes) => hashes.Where(hash => !ResourceExists(hash)).ToArray();
    }
}