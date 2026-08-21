using System;
using System.Text;

namespace oojjrs.oplat
{
    internal static class MyStorage
    {
        internal const int FileByteCountMax = 100 * 1024 * 1024;
        private const int FileNameByteCountMax = 259;

        internal static void EnsureData(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length > FileByteCountMax)
                throw new ArgumentOutOfRangeException(nameof(data), $"Storage files cannot exceed {FileByteCountMax} bytes.");
        }

        internal static void EnsureFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A storage file name is required.", nameof(fileName));

            if (Encoding.UTF8.GetByteCount(fileName) > FileNameByteCountMax)
                throw new ArgumentException($"A storage file name cannot exceed {FileNameByteCountMax} UTF-8 bytes.", nameof(fileName));

            if ((fileName[0] == '/') || (fileName[^1] == '/') || fileName.Contains("//") || fileName.Contains('\\'))
                throw new ArgumentException("A storage file name must be a portable relative path using single '/' separators.", nameof(fileName));

            foreach (var segment in fileName.Split('/'))
                EnsureFileNameSegment(segment, fileName);
        }

        private static void EnsureFileNameSegment(string segment, string fileName)
        {
            if ((segment == ".") || (segment == "..") || segment.EndsWith(' ') || segment.EndsWith('.'))
                throw new ArgumentException("A storage file name contains an invalid path segment.", nameof(fileName));

            foreach (var character in segment)
            {
                if ((character < 32) || (character == '<') || (character == '>') || (character == ':') || (character == '"') || (character == '|') || (character == '?') || (character == '*'))
                    throw new ArgumentException("A storage file name contains a character that is invalid on a supported platform.", nameof(fileName));
            }

            var periodIndex = segment.IndexOf('.');
            var baseName = (periodIndex < 0 ? segment : segment[..periodIndex]).ToUpperInvariant();
            if ((baseName == "CON") || (baseName == "PRN") || (baseName == "AUX") || (baseName == "NUL"))
                throw new ArgumentException("A storage file name contains a reserved path segment.", nameof(fileName));

            if ((baseName.Length == 4) && ((baseName.StartsWith("COM") || baseName.StartsWith("LPT"))) && (baseName[3] >= '1') && (baseName[3] <= '9'))
                throw new ArgumentException("A storage file name contains a reserved path segment.", nameof(fileName));
        }
    }
}
