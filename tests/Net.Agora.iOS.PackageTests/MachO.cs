using System.Buffers.Binary;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Just enough Mach-O to read a binary's symbol table: which symbols it exports, and which it
/// expects someone else to export.
/// </summary>
/// <remarks>
/// <para>
/// Shelling out to <c>nm</c> would be shorter and was the first attempt. It does not work here:
/// the package tests run on <c>ubuntu-latest</c> (only the pack job needs a Mac), and GNU
/// binutils' <c>nm</c> cannot read Mach-O — it reports "file format not recognized". Skipping the
/// check off-Mac would delete it precisely where it has to run, on every pull request, so the
/// parsing is done here instead. It is a fixed, documented format and the relevant part of it is
/// small.
/// </para>
/// <para>
/// Deliberately limited to 64-bit little-endian Mach-O, which is every slice these packages ship
/// (arm64 and x86_64). A fat archive is unwrapped first, since a simulator slice is one. Anything
/// else throws rather than returning an empty set — an empty set would read as "nothing missing"
/// and quietly pass the very test this exists for.
/// </para>
/// <para>
/// Verified against <c>nm -gU</c> and <c>nm -u</c> on every framework in these packages: same
/// symbol sets, exactly.
/// </para>
/// </remarks>
public static class MachO
{
    private const uint MagicFat = 0xCAFEBABE;      // big-endian by definition
    private const uint MagicFat64 = 0xCAFEBABF;
    private const uint Magic64 = 0xFEEDFACF;       // little-endian 64-bit
    private const uint LcSymtab = 0x2;
    private const uint LcLoadDylib = 0xC;

    // <mach-o/nlist.h>
    private const byte NTypeMask = 0x0E;
    private const byte NTypeUndefined = 0x00;
    private const byte NExternal = 0x01;

    /// <summary>Symbols this binary defines and makes visible — <c>nm -gU</c>.</summary>
    public static SortedSet<string> Exports(byte[] image) => Symbols(image, exported: true);

    /// <summary>Symbols this binary references and does not define — <c>nm -u</c>.</summary>
    public static SortedSet<string> Undefined(byte[] image) => Symbols(image, exported: false);

    /// <summary>
    /// The install names dyld must resolve before this binary can load — the non-weak entries of
    /// <c>otool -L</c>. LC_LOAD_WEAK_DYLIB is left out on purpose: a missing one is not fatal.
    /// </summary>
    public static SortedSet<string> RequiredLibraries(byte[] image)
    {
        var slice = FirstSlice(image);
        var libraries = new SortedSet<string>(StringComparer.Ordinal);

        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(slice + 16));
        var cursor = slice + 32;

        for (var i = 0; i < commandCount; i++)
        {
            var command = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor));
            var commandSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 4));

            if (command == LcLoadDylib)
            {
                // dylib_command: cmd, cmdsize, then dylib.name as an offset from this command.
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 8));
                libraries.Add(ReadCString(image, cursor + (int)nameOffset));
            }

            cursor += (int)commandSize;
        }

        return libraries;
    }

    private static SortedSet<string> Symbols(byte[] image, bool exported)
    {
        var slice = FirstSlice(image);
        var symbols = new SortedSet<string>(StringComparer.Ordinal);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(slice));
        if (magic != Magic64)
        {
            throw new InvalidDataException(
                $"not a 64-bit little-endian Mach-O (magic 0x{magic:x8}). These packages ship arm64 " +
                "and x86_64 only, so this means the payload layout changed.");
        }

        // mach_header_64: magic, cputype, cpusubtype, filetype, ncmds, sizeofcmds, flags, reserved.
        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(slice + 16));
        var cursor = slice + 32;

        for (var i = 0; i < commandCount; i++)
        {
            var command = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor));
            var commandSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 4));

            if (command == LcSymtab)
            {
                // symtab_command: cmd, cmdsize, symoff, nsyms, stroff, strsize — all file offsets
                // relative to the start of this slice, not the file.
                var symbolOffset = slice + (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 8));
                var symbolCount = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 12));
                var stringOffset = slice + (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(cursor + 16));

                for (var n = 0; n < symbolCount; n++)
                {
                    // nlist_64: n_strx (4), n_type (1), n_sect (1), n_desc (2), n_value (8).
                    var entry = symbolOffset + (n * 16);
                    var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(entry));
                    var type = image[entry + 4];

                    if ((type & NExternal) == 0)
                    {
                        continue;
                    }

                    var isUndefined = (type & NTypeMask) == NTypeUndefined;
                    if (isUndefined == exported || nameOffset == 0)
                    {
                        continue;
                    }

                    symbols.Add(ReadCString(image, stringOffset + (int)nameOffset));
                }

                return symbols;
            }

            cursor += (int)commandSize;
        }

        throw new InvalidDataException("Mach-O carries no LC_SYMTAB — the binary is stripped.");
    }

    /// <summary>
    /// The offset of the slice to read: 0 for a thin binary, or the first architecture of a fat
    /// one. Which architecture does not matter here — the question is what a framework imports
    /// and exports, and both are the same across a framework's slices by construction.
    /// </summary>
    private static int FirstSlice(byte[] image)
    {
        // A fat header is big-endian whatever the architectures inside it are.
        var magic = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(0));
        if (magic is not (MagicFat or MagicFat64))
        {
            return 0;
        }

        // fat_header: magic, nfat_arch. Then fat_arch: cputype, cpusubtype, offset, size, align —
        // the 64-bit variant widens offset/size, which moves nothing before them.
        return (int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(8 + 8));
    }

    private static string ReadCString(byte[] image, int offset)
    {
        var end = offset;
        while (end < image.Length && image[end] != 0)
        {
            end++;
        }

        return System.Text.Encoding.UTF8.GetString(image, offset, end - offset);
    }
}
