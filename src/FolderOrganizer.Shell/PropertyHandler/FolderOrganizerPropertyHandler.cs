// src/FolderOrganizer.Shell/PropertyHandler/FolderOrganizerPropertyHandler.cs
//
// Implements IPropertyStore + IInitializeWithFile so Explorer can display
// a "Organizer Tags" column sourced from the NTFS ADS metadata.
//
// COM registration adds a handler entry under:
//   HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PropertySystem\PropertyHandlers\*
//
// The property schema GUID matches the formatID in FolderOrganizer.propdesc.
// Install the property description via: PSCoInstall /s FolderOrganizer.propdesc
// (called by the MSI installer custom action).

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using FolderOrganizer.Core;
using FolderOrganizer.Core.Storage;

namespace FolderOrganizer.Shell.PropertyHandler
{
    // -------------------------------------------------------------------------
    // PROPERTYKEY: identifies a Windows Shell property
    // -------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid   fmtid;
        public uint   pid;
    }

    // -------------------------------------------------------------------------
    // PROPVARIANT: Win32 property variant (simplified — only VT_LPWSTR used here)
    // -------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT
    {
        public ushort vt;          // VARTYPE
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr data;        // union — for VT_LPWSTR this is a CoTaskMem LPWSTR pointer

        private const ushort VT_EMPTY  = 0;
        private const ushort VT_LPWSTR = 31;

        /// <summary>Creates a PROPVARIANT holding a LPWSTR string allocated in COM memory.</summary>
        public static PROPVARIANT FromString(string value)
        {
            var pv = new PROPVARIANT();
            if (value == null) return pv;
            pv.vt   = VT_LPWSTR;
            pv.data = Marshal.StringToCoTaskMemUni(value);
            return pv;
        }

        /// <summary>Frees COM-allocated memory in the variant. Call before the struct goes out of scope.</summary>
        public void Clear()
        {
            try
            {
                if (vt == VT_LPWSTR && data != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(data);
                    data = IntPtr.Zero;
                }
                vt = VT_EMPTY;
            }
            catch { }
        }
    }

    // -------------------------------------------------------------------------
    // IPropertyStore COM interface
    // -------------------------------------------------------------------------
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b7cdf620-db73-44c0-8611-832b261a0107")]
    internal interface IPropertyStore
    {
        uint GetCount();
        PROPERTYKEY GetAt(uint iProp);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        void Commit();
    }

    // -------------------------------------------------------------------------
    // IInitializeWithFile COM interface
    // -------------------------------------------------------------------------
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
    internal interface IInitializeWithFile
    {
        void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }

    // -------------------------------------------------------------------------
    // Property Handler implementation
    // -------------------------------------------------------------------------
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid(Com.Guids.PropertyHandler)]
    [ProgId("FolderOrganizer.PropertyHandler")]
    public class FolderOrganizerPropertyHandler : IPropertyStore, IInitializeWithFile
    {
        private string _filePath = string.Empty;
        private readonly AdsStorage _ads = new AdsStorage();

        // The PROPERTYKEY for FolderOrganizer.Tags — must match formatID in .propdesc
        private static readonly PROPERTYKEY TagsPropertyKey = new PROPERTYKEY
        {
            fmtid = new Guid(Com.Guids.PropertySchema),
            pid   = 1
        };

        #region IInitializeWithFile

        public void Initialize(string pszFilePath, uint grfMode)
        {
            try
            {
                _filePath = pszFilePath ?? string.Empty;
            }
            catch { }
        }

        #endregion

        #region IPropertyStore

        public uint GetCount()
        {
            try { return 1; }  // only FolderOrganizer.Tags
            catch { return 0; }
        }

        public PROPERTYKEY GetAt(uint iProp)
        {
            try
            {
                if (iProp == 0) return TagsPropertyKey;
            }
            catch { }
            return new PROPERTYKEY();
        }

        public void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv)
        {
            pv = new PROPVARIANT();
            try
            {
                if (key.fmtid == TagsPropertyKey.fmtid && key.pid == TagsPropertyKey.pid)
                {
                    var meta     = _ads.ReadMetadata(_filePath);
                    var tagNames = string.Join("; ", meta.Tags.Select(t => t.Name));
                    pv           = PROPVARIANT.FromString(tagNames);
                }
            }
            catch { }
        }

        // SetValue and Commit are intentionally no-ops:
        // Tags are written exclusively through the companion app / context menu,
        // not via the Explorer property-editing UI.
        public void SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar) { }

        public void Commit() { }

        #endregion

        #region COM Registration

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                // Register as a property handler for all file types (*)
                using (var key = Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\PropertySystem\PropertyHandlers\*"))
                    key?.SetValue("", "{" + Com.Guids.PropertyHandler + "}");
            }
            catch { }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\PropertySystem\PropertyHandlers\*",
                    throwOnMissingSubKey: false);
            }
            catch { }
        }

        #endregion
    }
}
