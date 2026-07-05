using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateResource(nint hUpdate, nint lpType, nint lpName, ushort wLanguage, byte[]? lpData, int cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndUpdateResource(nint hUpdate, [MarshalAs(UnmanagedType.Bool)] bool fDiscard);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint LoadLibraryEx(string lpFileName, nint hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(nint hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint FindResource(nint hModule, nint lpName, nint lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SizeofResource(nint hModule, nint hResInfo);

    public static readonly nint RT_ICON = 3;
    public static readonly nint RT_GROUP_ICON = (nint)14;
    public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
}
