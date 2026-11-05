namespace PhotoAlbum.Png;

/// <summary>
/// Adds a pHYs chunk to an encoded PNG so viewers, editors and print services report the intended print
/// resolution. Skia's PNG encoder does not write one. The pixel data is not touched.
/// </summary>
internal static class PngDpi
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // The signature (8 bytes) is followed by IHDR: length (4) + type (4) + data (13) + CRC (4).
    private const int AfterIhdr = 8 + 4 + 4 + 13 + 4;

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Embed(byte[] png, int dpi)
    {
        if (png.Length < AfterIhdr || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new PngExportException("The page image could not be encoded.");
        }

        var pixelsPerMetre = (int)Math.Round(dpi / 0.0254);

        // Chunk = length (4) + type (4) + data (9) + CRC (4); the CRC covers the type and the data.
        var chunk = new byte[4 + 4 + 9 + 4];
        WriteInt32(chunk, 0, 9);
        "pHYs"u8.CopyTo(chunk.AsSpan(4));
        WriteInt32(chunk, 8, pixelsPerMetre);
        WriteInt32(chunk, 12, pixelsPerMetre);
        chunk[16] = 1; // unit: metre
        WriteInt32(chunk, 17, (int)Crc32(chunk.AsSpan(4, 4 + 9)));

        var result = new byte[png.Length + chunk.Length];
        png.AsSpan(0, AfterIhdr).CopyTo(result);
        chunk.CopyTo(result.AsSpan(AfterIhdr));
        png.AsSpan(AfterIhdr).CopyTo(result.AsSpan(AfterIhdr + chunk.Length));
        return result;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
