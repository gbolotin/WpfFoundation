using System.Runtime.InteropServices;

namespace WpfFoundation.Notifications;

/// <summary>
/// The COM class Windows calls when a notification is clicked, from the pop-up or from Notification Center. It is
/// registered under the application's own activator CLSID while the application runs; after exit, Windows starts the
/// executable through that CLSID's <c>LocalServer32</c> key.
/// </summary>
internal sealed class ToastActivator : IDisposable
{
    private const uint LocalServer = 0x4;
    private const uint MultipleUse = 0x1;

    private readonly uint cookie;

    public ToastActivator(Guid clsid, Action<string> onActivated)
    {
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(ref clsid, new Factory(onActivated), LocalServer, MultipleUse, out cookie));
    }

    public void Dispose() => CoRevokeClassObject(cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(ref Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object factory, uint context, uint flags, out uint cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint cookie);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct UserInput
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Key;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Value;
    }

    [ComImport, Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface INotificationActivationCallback
    {
        void Activate(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] UserInput[] data,
            uint count);
    }

    [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(nint outer, ref Guid interfaceId, out nint instance);

        [PreserveSig]
        int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Callback(Action<string> onActivated) : INotificationActivationCallback
    {
        public void Activate(string appUserModelId, string invokedArgs, UserInput[] data, uint count) => onActivated(invokedArgs);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Factory(Action<string> onActivated) : IClassFactory
    {
        private const int NoAggregation = unchecked((int)0x80040110);
        private const int NoInterface = unchecked((int)0x80004002);
        private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");

        public int CreateInstance(nint outer, ref Guid interfaceId, out nint instance)
        {
            instance = 0;
            if (outer != 0)
            {
                return NoAggregation;
            }

            if (interfaceId != typeof(INotificationActivationCallback).GUID && interfaceId != UnknownId)
            {
                return NoInterface;
            }

            instance = Marshal.GetComInterfaceForObject(new Callback(onActivated), typeof(INotificationActivationCallback));
            return 0;
        }

        public int LockServer(bool locked) => 0;
    }
}
