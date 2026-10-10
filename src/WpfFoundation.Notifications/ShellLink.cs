using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfFoundation.Notifications;

/// <summary>Reads and writes a Start menu shortcut with the AppUserModelId and toast activator properties.</summary>
internal static class ShellLink
{
    private static readonly Guid AppUserModelFormat = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint AppUserModelIdProperty = 5;
    private const uint ToastActivatorClsidProperty = 26;
    private const ushort VtLpwstr = 31;
    private const ushort VtClsid = 72;

    /// <summary>What an existing shortcut points to and carries.</summary>
    public sealed record Contents(string TargetPath, string? AppId, Guid? ActivatorClsid);

    public static Contents? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var link = CreateLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, 0, 0);
            var store = (IPropertyStore)link;
            return new Contents(target.ToString(), ReadString(store, AppUserModelIdProperty), ReadClsid(store, ToastActivatorClsidProperty));
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    public static void Write(string path, string targetPath, string appId, Guid activatorClsid)
    {
        var link = CreateLink();
        try
        {
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            var store = (IPropertyStore)link;
            var appIdValue = new PropVariant { Type = VtLpwstr, Pointer = Marshal.StringToCoTaskMemUni(appId) };
            SetAndClear(store, AppUserModelIdProperty, ref appIdValue);
            var clsidValue = new PropVariant { Type = VtClsid, Pointer = Marshal.AllocCoTaskMem(16) };
            Marshal.Copy(activatorClsid.ToByteArray(), 0, clsidValue.Pointer, 16);
            SetAndClear(store, ToastActivatorClsidProperty, ref clsidValue);
            store.Commit();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ((IPersistFile)link).Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    private static IShellLinkW CreateLink() =>
        (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), throwOnError: true)!)!;

    private static void SetAndClear(IPropertyStore store, uint id, ref PropVariant value)
    {
        var key = new PropertyKey { Format = AppUserModelFormat, Id = id };
        try
        {
            store.SetValue(ref key, ref value);
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static string? ReadString(IPropertyStore store, uint id)
    {
        var value = Get(store, id);
        try
        {
            return value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static Guid? ReadClsid(IPropertyStore store, uint id)
    {
        var value = Get(store, id);
        try
        {
            return value.Type == VtClsid ? Marshal.PtrToStructure<Guid>(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static PropVariant Get(IPropertyStore store, uint id)
    {
        var key = new PropertyKey { Format = AppUserModelFormat, Id = id };
        store.GetValue(ref key, out var value);
        return value;
    }

    [DllImport("ole32.dll", PreserveSig = false)]
    private static extern void PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    // Only the type and the pointer member are used; the size covers the whole union on 64-bit.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort Type;

        [FieldOffset(8)]
        public nint Pointer;
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int length, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
        void GetCurFile(out nint file);
    }
}
