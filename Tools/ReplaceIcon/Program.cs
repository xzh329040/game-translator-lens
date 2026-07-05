using System.Runtime.InteropServices;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ReplaceIcon.exe <target.exe> <icon.ico>");
    Console.Error.WriteLine("  Replaces the MAINICON resource in target.exe with icon.ico");
    Console.Error.WriteLine("  Creates target.exe.backup before modification.");
    return 1;
}

string exePath = Path.GetFullPath(args[0]);
string icoPath = Path.GetFullPath(args[1]);

if (!File.Exists(exePath))
{
    Console.Error.WriteLine($"ERROR: Target exe not found: {exePath}");
    return 1;
}
if (!File.Exists(icoPath))
{
    Console.Error.WriteLine($"ERROR: Icon file not found: {icoPath}");
    return 1;
}

Console.WriteLine($"Target:  {exePath}");
Console.WriteLine($"Icon:    {icoPath}");

// Read and parse the .ico file
byte[] icoData = File.ReadAllBytes(icoPath);
if (icoData.Length < 6)
{
    Console.Error.WriteLine("ERROR: .ico file too small");
    return 1;
}

ushort reserved = BitConverter.ToUInt16(icoData, 0);
ushort type = BitConverter.ToUInt16(icoData, 2);
ushort count = BitConverter.ToUInt16(icoData, 4);

if (reserved != 0 || type != 1)
{
    Console.Error.WriteLine($"ERROR: Not a valid .ico file (reserved={reserved}, type={type})");
    return 1;
}

Console.WriteLine($"Images in .ico: {count}");

// Parse each icon entry: (width, height, colorCount, planes, bpp, data)
var entries = new List<(byte width, byte height, byte colorCount, ushort planes, ushort bpp, byte[] data)>();
for (int i = 0; i < count; i++)
{
    int entryOffset = 6 + i * 16;
    if (entryOffset + 16 > icoData.Length)
    {
        Console.Error.WriteLine($"ERROR: .ico entry {i} header out of bounds");
        return 1;
    }

    byte w = icoData[entryOffset];
    byte h = icoData[entryOffset + 1];
    byte cc = icoData[entryOffset + 2];
    ushort planes = BitConverter.ToUInt16(icoData, entryOffset + 4);
    ushort bpp = BitConverter.ToUInt16(icoData, entryOffset + 6);
    uint size = BitConverter.ToUInt32(icoData, entryOffset + 8);
    uint offset = BitConverter.ToUInt32(icoData, entryOffset + 12);

    if (offset + size > icoData.Length)
    {
        Console.Error.WriteLine($"ERROR: .ico entry {i} image data out of bounds (offset={offset}, size={size})");
        return 1;
    }

    byte[] imgData = icoData[(int)offset..(int)(offset + size)];

    int displayW = w == 0 ? 256 : w;
    int displayH = h == 0 ? 256 : h;
    Console.WriteLine($"  [{i}] {displayW}x{displayH} @{bpp}bpp ({size} bytes) -> RT_ICON ID={i + 1}");

    entries.Add((w, h, cc, planes, bpp, imgData));
}

// Create backup
string backupPath = exePath + ".backup";
File.Copy(exePath, backupPath, true);
Console.WriteLine($"Backup:  {backupPath}");

// Begin resource update (preserve existing non-icon resources like manifest/version)
nint hUpdate = NativeMethods.BeginUpdateResource(exePath, false);
if (hUpdate == nint.Zero)
{
    int err = Marshal.GetLastWin32Error();
    Console.Error.WriteLine($"ERROR: BeginUpdateResource failed, Win32 error: {err}");
    return 1;
}

try
{
    // Step 1: Add each image as a separate RT_ICON resource
    for (int i = 0; i < entries.Count; i++)
    {
        nint iconId = i + 1;
        if (!NativeMethods.UpdateResource(hUpdate, NativeMethods.RT_ICON, iconId, 0,
                entries[i].data, entries[i].data.Length))
        {
            int err = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"ERROR: UpdateResource RT_ICON #{i + 1} failed, Win32 error: {err}");
            return 1;
        }
    }

    // Step 2: Build RT_GROUP_ICON directory
    // GRPICONDIR: 6-byte header + N * 14-byte GRPICONDIRENTRY
    int groupIconSize = 6 + entries.Count * 14;
    byte[] groupIconData = new byte[groupIconSize];

    // Copy ICO header bytes (reserved=0, type=1, count)
    Array.Copy(icoData, 0, groupIconData, 0, 6);

    // Build directory entries (each GRPICONDIRENTRY is 14 bytes:
    //   first 12 bytes match ICO entry, last 2 bytes = RT_ICON resource ID)
    for (int i = 0; i < entries.Count; i++)
    {
        int srcEntryOffset = 6 + i * 16;
        int dstEntryOffset = 6 + i * 14;

        // Copy first 12 bytes from ICO directory entry
        Array.Copy(icoData, srcEntryOffset, groupIconData, dstEntryOffset, 12);

        // Set resource ID (WORD, little-endian)
        ushort resId = (ushort)(i + 1);
        BitConverter.GetBytes(resId).CopyTo(groupIconData, dstEntryOffset + 12);
    }

    // Write RT_GROUP_ICON with name = 1 (MAINICON)
    nint mainIconName = 1;
    if (!NativeMethods.UpdateResource(hUpdate, NativeMethods.RT_GROUP_ICON, mainIconName, 0,
            groupIconData, groupIconData.Length))
    {
        int err = Marshal.GetLastWin32Error();
        Console.Error.WriteLine($"ERROR: UpdateResource RT_GROUP_ICON failed, Win32 error: {err}");
        return 1;
    }

    // Commit
    if (!NativeMethods.EndUpdateResource(hUpdate, false))
    {
        int err = Marshal.GetLastWin32Error();
        Console.Error.WriteLine($"ERROR: EndUpdateResource failed, Win32 error: {err}");
        return 1;
    }

    Console.WriteLine($"SUCCESS: {entries.Count} icons embedded into {exePath}");
    Console.WriteLine($"  Original backed up to {backupPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    NativeMethods.EndUpdateResource(hUpdate, true);
    return 1;
}
