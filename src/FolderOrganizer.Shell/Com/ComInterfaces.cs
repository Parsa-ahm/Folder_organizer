// src/FolderOrganizer.Shell/Com/ComInterfaces.cs
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FolderOrganizer.Shell.Com
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e8-0000-0000-c000-000000000046")]
    internal interface IShellExtInit
    {
        void Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    internal interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214f4-0000-0000-c000-000000000046")]
    internal interface IContextMenu2 : IContextMenu
    {
        [PreserveSig]
        new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        new void InvokeCommand(IntPtr pici);

        new void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);

        void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }
}
