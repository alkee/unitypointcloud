using System;
using System.Runtime.InteropServices;

namespace upc
{
    public class WinApi
    {
        #region Win32 API bindings

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class FileDlg
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public String filter = null;
            public String customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public String file = null;
            public int maxFile = 0;
            public String fileTitle = null;
            public int maxFileTitle = 0;
            public String initialDir = null;
            public String title = null;
            public Flag flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public String defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public String templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;

            [Flags]
            public enum Flag
            {
                OFN_ALLOWMULTISELECT = 0x00000200,
                OFN_CREATEPROMPT = 0x00002000,
                OFN_DONTADDTORECENT = 0x02000000,
                OFN_ENABLEHOOK = 0x00000020,
                OFN_ENABLEINCLUDENOTIFY = 0x00400000,
                OFN_ENABLESIZING = 0x00800000,
                OFN_ENABLETEMPLATE = 0x00000040,
                OFN_ENABLETEMPLATEHANDLE = 0x00000080,
                OFN_EXPLORER = 0x00080000,
                OFN_EXTENSIONDIFFERENT = 0x00000400,
                OFN_FILEMUSTEXIST = 0x00001000,
                OFN_FORCESHOWHIDDEN = 0x10000000,
                OFN_HIDEREADONLY = 0x00000004,
                OFN_LONGNAMES = 0x00200000,
                OFN_NOCHANGEDIR = 0x00000008,
                OFN_NODEREFERENCELINKS = 0x00100000,
                OFN_NOLONGNAMES = 0x00040000,
                OFN_NONETWORKBUTTON = 0x00020000,
                OFN_NOREADONLYRETURN = 0x00008000,
                OFN_NOTESTFILECREATE = 0x00010000,
                OFN_NOVALIDATE = 0x00000100,
                OFN_OVERWRITEPROMPT = 0x00000002,
                OFN_PATHMUSTEXIST = 0x00000800,
                OFN_READONLY = 0x00000001,
                OFN_SHAREAWARE = 0x00004000,
                OFN_SHOWHELP = 0x00000010
            }
        }

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] FileDlg ofd);

        //[DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        //public static extern bool GetSaveFileName([In, Out] FileDlg ofd);

        #endregion Win32 API bindings

        #region Highlevel helpers

        public class FileOpenDialogFilter
        {
            public string Title;
            public string[] FilePattern; // ex) "*.obj", "*.jpg"

            public FileOpenDialogFilter(string title, params string[] filePattern)
            {
                Title = title;
                FilePattern = filePattern;
            }

            public static string ToWinApiString(FileOpenDialogFilter[] filters)
            { // https://docs.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamea
                // 형식은 description + filter + ( + description + filter ... ) 와 같은 조합이고
                //    각 연결은 null(\0)을 이용. filter 내 여러 연결은 ; 문자 이용
                //    ex) "text files (.txt, .text)\0*.txt;*.text\0document files (.doc, .docx)\0*.doc;*.docx\0\0"
                var result = "";
                foreach (var f in filters)
                {
                    result += f.Title + "\0";
                    result += string.Join(";", f.FilePattern) + "\0";
                }
                result += "\0"; // end of filter
                return result;
            }
        }

        /// <summary>
        ///     GetOpenFileName 의 high level wrapper
        /// </summary>
        /// <param name="initialDir">시작 directory</param>
        /// <param name="title">open file dialog title</param>
        /// <param name="multipleSelection">여러파일 선택여부</param>
        /// <param name="filters">dialog 에 보여질 파일들 종류</param>
        /// <returns>취소된 경우 null</returns>
        public static string FileOpenDialog(string initialDir, string title, bool multipleSelection = false, params FileOpenDialogFilter[] filters)
        {
            const int MAX_STRING_SIZE = 512;

            var filter = FileOpenDialogFilter.ToWinApiString(filters);
            var flags = FileDlg.Flag.OFN_NOCHANGEDIR // 선택한 경로로 current dir 변경 ; UNITY crash 발생하므로 필수 flag
                                                     // | FileDlg.Flag.OFN_EXTENSIONDIFFERENT
                | FileDlg.Flag.OFN_EXPLORER | FileDlg.Flag.OFN_FILEMUSTEXIST | FileDlg.Flag.OFN_PATHMUSTEXIST;
            if (multipleSelection) flags |= FileDlg.Flag.OFN_ALLOWMULTISELECT;
            var param = new FileDlg
            {
                // out values
                file = new string(new char[MAX_STRING_SIZE]), // full file path ; out value
                maxFile = MAX_STRING_SIZE,
                fileTitle = new string(new char[MAX_STRING_SIZE]), // file name.ext ; out value
                maxFileTitle = MAX_STRING_SIZE,

                initialDir = initialDir,
                title = title,
                filter = filter,
                defExt = null,
                flags = flags
            };
            param.structSize = Marshal.SizeOf(param);
            if (GetOpenFileName(param))
            {
                return param.file;
            }
            return null;
        }

        #endregion Highlevel helpers
    }
}