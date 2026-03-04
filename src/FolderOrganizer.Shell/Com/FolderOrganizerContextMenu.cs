// src/FolderOrganizer.Shell/Com/FolderOrganizerContextMenu.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FolderOrganizer.Core;
using FolderOrganizer.Core.Icons;
using FolderOrganizer.Core.Models;
using FolderOrganizer.Core.Storage;
using FolderOrganizer.Core.Tags;
using FolderOrganizer.Shell.Drawing;

namespace FolderOrganizer.Shell.Com
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid(Guids.ContextMenu)]
    [ProgId("FolderOrganizer.ContextMenu")]
    public class FolderOrganizerContextMenu : IShellExtInit, IContextMenu2
    {
        private string _selectedPath = string.Empty;
        private readonly AdsStorage _ads = new AdsStorage();
        private readonly TagRegistry _tagRegistry = new TagRegistry(AppDataPaths.RootDir);
        private readonly IconCache _iconCache = new IconCache(AppDataPaths.RootDir);

        // Loaded at QueryContextMenu time
        private List<TagEntry> _loadedTags = new List<TagEntry>();
        private List<string> _iconFiles = new List<string>();

        // Command offsets from idCmdFirst
        private const int CMD_ORGANIZE    = 0;  // top-level submenu parent
        private const int CMD_COLOR_ROW   = 1;  // owner-drawn color circles row
        private const int CMD_ICON_ROW    = 2;  // owner-drawn icon grid row
        private const int CMD_TAG_ROW     = 3;  // owner-drawn tag pills row
        private const int CMD_SEP         = 4;  // separator (not invocable)
        private const int CMD_CLEAR_COLOR = 5;
        private const int CMD_CLEAR_ICON  = 6;
        private const int CMD_CLEAR_TAGS  = 7;
        private const int CMD_CLEAR_ALL   = 8;
        private const int CMD_TAG_BASE    = 100; // tag toggles start at offset 100

        // Track the idCmdFirst passed to QueryContextMenu so InvokeCommand can subtract it
        private uint _idCmdFirst;

        #region IShellExtInit

        public void Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID)
        {
            try
            {
                if (pDataObj == IntPtr.Zero)
                {
                    _selectedPath = GetPathFromPidl(pidlFolder);
                    return;
                }

                var dataObj = Marshal.GetObjectForIUnknown(pDataObj)
                    as System.Runtime.InteropServices.ComTypes.IDataObject;
                if (dataObj == null) return;

                _selectedPath = GetPathFromDataObject(dataObj);
            }
            catch { /* never propagate into Explorer */ }
        }

        #endregion

        #region IContextMenu2

        public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
        {
            try
            {
                // CMF_DEFAULTONLY — only the default action is needed, don't add items
                if ((uFlags & 0xF) == 0x10)
                    return WinError.MAKE_HRESULT(0, 0, 0);

                _idCmdFirst = idCmdFirst;

                // Pre-load tags and icon files so owner-draw can reference them
                try { _loadedTags = _tagRegistry.GetAll(); } catch { _loadedTags = new List<TagEntry>(); }
                try
                {
                    _iconFiles = Directory.Exists(AppDataPaths.CustomIconsDir)
                        ? Directory.GetFiles(AppDataPaths.CustomIconsDir, "*.ico")
                              .Concat(Directory.GetFiles(AppDataPaths.CustomIconsDir, "*.png"))
                              .ToList()
                        : new List<string>();
                }
                catch { _iconFiles = new List<string>(); }

                var hSubMenu = CreatePopupMenu();

                // --- Color row (owner-drawn) ---
                var colorMii = new MENUITEMINFO();
                colorMii.cbSize    = (uint)Marshal.SizeOf(colorMii);
                colorMii.fMask     = MIIM_ID | MIIM_TYPE | MIIM_DATA;
                colorMii.fType     = MFT_OWNERDRAW;
                colorMii.wID       = idCmdFirst + CMD_COLOR_ROW;
                colorMii.dwItemData = (IntPtr)CMD_COLOR_ROW;
                InsertMenuItemW(hSubMenu, 0, true, ref colorMii);

                // --- Icon row (owner-drawn) ---
                var iconMii = new MENUITEMINFO();
                iconMii.cbSize    = (uint)Marshal.SizeOf(iconMii);
                iconMii.fMask     = MIIM_ID | MIIM_TYPE | MIIM_DATA;
                iconMii.fType     = MFT_OWNERDRAW;
                iconMii.wID       = idCmdFirst + CMD_ICON_ROW;
                iconMii.dwItemData = (IntPtr)CMD_ICON_ROW;
                InsertMenuItemW(hSubMenu, 1, true, ref iconMii);

                // --- Tag row (owner-drawn) ---
                var tagMii = new MENUITEMINFO();
                tagMii.cbSize    = (uint)Marshal.SizeOf(tagMii);
                tagMii.fMask     = MIIM_ID | MIIM_TYPE | MIIM_DATA;
                tagMii.fType     = MFT_OWNERDRAW;
                tagMii.wID       = idCmdFirst + CMD_TAG_ROW;
                tagMii.dwItemData = (IntPtr)CMD_TAG_ROW;
                InsertMenuItemW(hSubMenu, 2, true, ref tagMii);

                // --- Separator ---
                InsertMenu(hSubMenu, 3, MF_BYPOSITION | MF_SEPARATOR, UIntPtr.Zero, null);

                // --- Clear items ---
                InsertMenu(hSubMenu, 4, MF_BYPOSITION | MF_STRING, (UIntPtr)(idCmdFirst + CMD_CLEAR_COLOR), "Clear Color");
                InsertMenu(hSubMenu, 5, MF_BYPOSITION | MF_STRING, (UIntPtr)(idCmdFirst + CMD_CLEAR_ICON),  "Clear Icon");
                InsertMenu(hSubMenu, 6, MF_BYPOSITION | MF_STRING, (UIntPtr)(idCmdFirst + CMD_CLEAR_TAGS),  "Clear Tags");
                InsertMenu(hSubMenu, 7, MF_BYPOSITION | MF_STRING, (UIntPtr)(idCmdFirst + CMD_CLEAR_ALL),   "Clear All");

                // --- Top-level "Organize" item with submenu ---
                var topMii = new MENUITEMINFO();
                topMii.cbSize    = (uint)Marshal.SizeOf(topMii);
                topMii.fMask     = MIIM_SUBMENU | MIIM_STRING | MIIM_ID;
                topMii.wID       = idCmdFirst + CMD_ORGANIZE;
                topMii.hSubMenu  = hSubMenu;
                topMii.dwTypeData = "Organize";
                InsertMenuItemW(hmenu, indexMenu, true, ref topMii);

                // Return number of commands added (highest offset + 1)
                return WinError.MAKE_HRESULT(0, 0, CMD_CLEAR_ALL + 1);
            }
            catch
            {
                return WinError.MAKE_HRESULT(1, 0, 0);
            }
        }

        public void InvokeCommand(IntPtr pici)
        {
            try
            {
                var ici = (CMINVOKECOMMANDINFO)Marshal.PtrToStructure(pici, typeof(CMINVOKECOMMANDINFO));

                // If the high word of lpVerb is non-zero it is a string verb pointer, not a numeric offset
                if ((ici.lpVerb.ToInt64() >> 16) != 0)
                    return;

                int cmdOffset = ici.lpVerb.ToInt32();

                switch (cmdOffset)
                {
                    case CMD_COLOR_ROW:
                        // Direct click on the row itself — launch app color picker
                        LaunchApp($"--action setcolor --path \"{_selectedPath}\"");
                        break;

                    case CMD_ICON_ROW:
                        LaunchApp($"--action seticon --path \"{_selectedPath}\"");
                        break;

                    case CMD_TAG_ROW:
                        LaunchApp($"--action settags --path \"{_selectedPath}\"");
                        break;

                    case CMD_CLEAR_COLOR:
                        _ads.ClearField(_selectedPath, "color");
                        ApplyFolderIcon(_selectedPath, _ads.ReadMetadata(_selectedPath));
                        break;

                    case CMD_CLEAR_ICON:
                        _ads.ClearField(_selectedPath, "icon");
                        ApplyFolderIcon(_selectedPath, _ads.ReadMetadata(_selectedPath));
                        break;

                    case CMD_CLEAR_TAGS:
                        _ads.ClearField(_selectedPath, "tags");
                        break;

                    case CMD_CLEAR_ALL:
                        _ads.ClearMetadata(_selectedPath);
                        ApplyFolderIcon(_selectedPath, new ItemMetadata());
                        break;

                    default:
                        // Tag toggles: CMD_TAG_BASE + tagIndex
                        if (cmdOffset >= CMD_TAG_BASE)
                        {
                            int tagIndex = cmdOffset - CMD_TAG_BASE;
                            if (tagIndex < _loadedTags.Count)
                            {
                                var meta = _ads.ReadMetadata(_selectedPath);
                                var tagName = _loadedTags[tagIndex].Name;
                                var existing = meta.Tags.FirstOrDefault(t => t.Name == tagName);
                                if (existing != null)
                                    meta.Tags.Remove(existing);
                                else
                                    meta.Tags.Add(_loadedTags[tagIndex]);
                                _ads.WriteMetadata(_selectedPath, meta);
                                ApplyFolderIcon(_selectedPath, meta);
                            }
                        }

                        // Icon grid: CMD_ICON_ROW+1 .. CMD_ICON_ROW+iconCount
                        // handled via direct hit-test in owner-draw click — not mapped here
                        break;
                }
            }
            catch { }
        }

        public void GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax)
        {
            try
            {
                if ((uType & 0x4) != 0) // GCS_UNICODE
                    pszName.Append("FolderOrganizer");
            }
            catch { }
        }

        public void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                const uint WM_MEASUREITEM = 0x002C;
                const uint WM_DRAWITEM    = 0x002B;

                if (uMsg == WM_MEASUREITEM)
                {
                    HandleMeasureItem(lParam);
                }
                else if (uMsg == WM_DRAWITEM)
                {
                    HandleDrawItem(lParam);
                }
            }
            catch { }
        }

        #endregion

        #region Owner-Draw Helpers

        private void HandleMeasureItem(IntPtr lParam)
        {
            try
            {
                var mis = (MEASUREITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(MEASUREITEMSTRUCT));
                int itemData = mis.itemData.ToInt32();

                System.Drawing.Size size;
                if (itemData == CMD_COLOR_ROW)
                    size = MenuRenderer.MeasureColorRow();
                else if (itemData == CMD_ICON_ROW)
                    size = MenuRenderer.MeasureIconRow(_iconFiles.Count);
                else if (itemData == CMD_TAG_ROW)
                    size = MenuRenderer.MeasureTagRow(_loadedTags.Count);
                else
                    return;

                mis.itemWidth  = (uint)size.Width;
                mis.itemHeight = (uint)size.Height;
                Marshal.StructureToPtr(mis, lParam, true);
            }
            catch { }
        }

        private void HandleDrawItem(IntPtr lParam)
        {
            try
            {
                var dis = (DRAWITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(DRAWITEMSTRUCT));
                int itemData = dis.itemData.ToInt32();

                var bounds = new System.Drawing.Rectangle(
                    dis.rcItem.left, dis.rcItem.top,
                    dis.rcItem.right - dis.rcItem.left,
                    dis.rcItem.bottom - dis.rcItem.top);

                string activeColor = null;
                List<string> activeTags = new List<string>();
                try
                {
                    var meta = _ads.ReadMetadata(_selectedPath);
                    activeColor = meta.Color;
                    activeTags = meta.Tags.Select(t => t.Name).ToList();
                }
                catch { }

                if (itemData == CMD_COLOR_ROW)
                {
                    MenuRenderer.DrawColorRow(dis.hDC, bounds, activeColor);
                }
                else if (itemData == CMD_ICON_ROW)
                {
                    MenuRenderer.DrawIconRow(dis.hDC, bounds, _iconFiles);
                }
                else if (itemData == CMD_TAG_ROW)
                {
                    MenuRenderer.DrawTagRow(dis.hDC, bounds, _loadedTags, activeTags);
                }
            }
            catch { }
        }

        #endregion

        #region Apply Folder Icon via desktop.ini

        private void ApplyFolderIcon(string folderPath, ItemMetadata meta)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return;

                string iconPath = null;
                try
                {
                    iconPath = meta.IconPath
                        ?? (meta.Color != null ? _iconCache.GetOrGenerateIcon(meta.Color) : null);
                }
                catch { }

                var desktopIni = Path.Combine(folderPath, "desktop.ini");

                if (iconPath == null)
                {
                    // Remove icon customization from desktop.ini
                    if (File.Exists(desktopIni))
                    {
                        try
                        {
                            RemoveFileAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
                            var lines = File.ReadAllLines(desktopIni)
                                .Where(l => !l.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)
                                         && !l.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase)
                                         && l.Trim() != "[.ShellClassInfo]")
                                .ToArray();
                            if (lines.Length == 0)
                                File.Delete(desktopIni);
                            else
                                File.WriteAllLines(desktopIni, lines);
                        }
                        catch { }
                    }
                    try
                    {
                        RemoveFileAttributes(folderPath, FileAttributes.System);
                    }
                    catch { }

                    try { SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH, folderPath, null); } catch { }
                    return;
                }

                // Write desktop.ini
                try
                {
                    // Remove hidden/system so we can write
                    if (File.Exists(desktopIni))
                        RemoveFileAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);

                    var ini = "[.ShellClassInfo]\r\nIconFile=" + iconPath + "\r\nIconIndex=0\r\n";
                    File.WriteAllText(desktopIni, ini, Encoding.Unicode);

                    // Re-apply hidden+system attributes
                    File.SetAttributes(desktopIni,
                        File.GetAttributes(desktopIni) | FileAttributes.Hidden | FileAttributes.System);

                    // Folder must have System attribute for desktop.ini to take effect
                    var folderAttrs = File.GetAttributes(folderPath);
                    if ((folderAttrs & FileAttributes.System) == 0)
                        File.SetAttributes(folderPath, folderAttrs | FileAttributes.System);
                }
                catch { }

                try { SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH, folderPath, null); } catch { }
            }
            catch { }
        }

        private static void RemoveFileAttributes(string path, FileAttributes attrs)
        {
            try
            {
                var current = File.GetAttributes(path);
                File.SetAttributes(path, current & ~attrs);
            }
            catch { }
        }

        #endregion

        #region COM Registration

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                // Files
                using (var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
                    @"*\shellex\ContextMenuHandlers\FolderOrganizer"))
                    key?.SetValue("", "{" + Guids.ContextMenu + "}");

                // Folders
                using (var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
                    @"Folder\shellex\ContextMenuHandlers\FolderOrganizer"))
                    key?.SetValue("", "{" + Guids.ContextMenu + "}");

                // Directories
                using (var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(
                    @"Directory\shellex\ContextMenuHandlers\FolderOrganizer"))
                    key?.SetValue("", "{" + Guids.ContextMenu + "}");
            }
            catch { }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
                    @"*\shellex\ContextMenuHandlers\FolderOrganizer", false);
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
                    @"Folder\shellex\ContextMenuHandlers\FolderOrganizer", false);
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKey(
                    @"Directory\shellex\ContextMenuHandlers\FolderOrganizer", false);
            }
            catch { }
        }

        #endregion

        #region Helpers

        private static void LaunchApp(string args)
        {
            try
            {
                var appPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "FolderOrganizer", "FolderOrganizer.App.exe");
                if (File.Exists(appPath))
                    System.Diagnostics.Process.Start(appPath, args);
            }
            catch { }
        }

        private static string GetPathFromPidl(IntPtr pidl)
        {
            try
            {
                var sb = new StringBuilder(260);
                SHGetPathFromIDList(pidl, sb);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        private static string GetPathFromDataObject(
            System.Runtime.InteropServices.ComTypes.IDataObject dataObj)
        {
            try
            {
                var fmt = new System.Runtime.InteropServices.ComTypes.FORMATETC
                {
                    cfFormat = (short)CF_HDROP,
                    ptd      = IntPtr.Zero,
                    dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                    lindex   = -1,
                    tymed    = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
                };
                dataObj.GetData(ref fmt, out var stg);
                var sb = new StringBuilder(260);
                DragQueryFile(stg.unionmember, 0, sb, 260);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        #endregion

        #region P/Invoke

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags,
            [MarshalAs(UnmanagedType.LPWStr)] string dwItem1,
            [MarshalAs(UnmanagedType.LPWStr)] string dwItem2);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags,
            UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenuItemW(IntPtr hMenu, uint uItem, bool fByPosition,
            ref MENUITEMINFO lpmii);

        // Windows message constants
        private const ushort CF_HDROP = 15;

        // Menu flags
        private const uint MF_BYPOSITION = 0x00000400;
        private const uint MF_STRING     = 0x00000000;
        private const uint MF_SEPARATOR  = 0x00000800;
        private const uint MFT_OWNERDRAW = 0x00000100;

        // MENUITEMINFO fMask values
        private const uint MIIM_ID      = 0x00000002;
        private const uint MIIM_TYPE    = 0x00000010;
        private const uint MIIM_DATA    = 0x00000020;
        private const uint MIIM_SUBMENU = 0x00000004;
        private const uint MIIM_STRING  = 0x00000040;

        // SHChangeNotify constants
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATH      = 0x00000005;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MENUITEMINFO
        {
            public uint    cbSize;
            public uint    fMask;
            public uint    fType;
            public uint    fState;
            public uint    wID;
            public IntPtr  hSubMenu;
            public IntPtr  hbmpChecked;
            public IntPtr  hbmpUnchecked;
            public IntPtr  dwItemData;
            public string  dwTypeData;
            public uint    cch;
            public IntPtr  hbmpItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CMINVOKECOMMANDINFO
        {
            public uint    cbSize;
            public uint    fMask;
            public IntPtr  hwnd;
            public IntPtr  lpVerb;
            public IntPtr  lpParameters;
            public IntPtr  lpDirectory;
            public int     nShow;
            public uint    dwHotKey;
            public IntPtr  hIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEASUREITEMSTRUCT
        {
            public uint    CtlType;
            public uint    CtlID;
            public uint    itemID;
            public uint    itemWidth;
            public uint    itemHeight;
            public IntPtr  itemData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DRAWITEMSTRUCT
        {
            public uint    CtlType;
            public uint    CtlID;
            public uint    itemID;
            public uint    itemAction;
            public uint    itemState;
            public IntPtr  hwndItem;
            public IntPtr  hDC;
            public RECT    rcItem;
            public IntPtr  itemData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        #endregion
    }

    internal static class WinError
    {
        public static int MAKE_HRESULT(uint sev, uint fac, uint code) =>
            unchecked((int)((sev << 31) | (fac << 16) | code));
    }
}
