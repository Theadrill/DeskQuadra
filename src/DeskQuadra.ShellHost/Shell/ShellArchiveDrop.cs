using System.Runtime.InteropServices;
using System.Text;
using DeskQuadra.Core.FileSystem;
using DeskQuadra.Core.ThirdParty;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace DeskQuadra.ShellHost.Shell;

// F3 drop-em-container (docs/PLANO_DROP_CONTAINER.md): encaminha arquivos para
// o DropHandler registrado do container (zip nativo, WinRAR, 7-Zip) via
// IShellFolder.GetUIObjectOf(IID_IDropTarget) + IDataObject CF_HDROP — o mesmo
// que o Explorer faz ao soltar sobre o arquivo. Sem codec próprio (RAR é
// proprietário RarLab); sem handler = false silencioso (a F4 assume).
// Roda isolado no ShellHost (STA do ShellStaRunner): handler de terceiro que
// trava nunca congela a UI — timeout/kill no cliente, padrão T5.
internal static class ShellArchiveDrop
{
    private const short CF_HDROP = 15;
    private const uint MK_LBUTTON = 0x0001;
    private const uint DROPEFFECT_COPY = 1;
    private const int S_OK = 0;
    private const int S_FALSE_OLE_ALREADY_INITIALIZED = 1;
    private const int DV_E_FORMATETC = unchecked((int)0x80040064);
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const int DATA_S_SAMEFORMATETC = 0x00040130;

    public static bool TryDrop(string containerPath, IReadOnlyList<string> files)
    {
        if (!ArchiveFormatDetector.IsContainer(containerPath))
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(containerPath, "(drop)", 0, "n/a", "n/a", "n/a", "not-a-container"));
            return false;
        }

        var existing = files
            .Where(f => !string.IsNullOrWhiteSpace(f) && (File.Exists(f) || Directory.Exists(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (existing.Count == 0)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(containerPath, "(drop)", 0, "n/a", "n/a", "n/a", "no-existing-sources"));
            return false;
        }

        return ShellStaRunner.TryRun(
            () => DropOnStaThread(containerPath, existing),
            ShellHostProtocol.DropTimeoutMs,
            "DeskQuadra.ShellArchiveDrop",
            containerPath);
    }

    private static bool DropOnStaThread(string containerPath, List<string> files)
    {
        // Diagnóstico: o TryRun engoliria a exceção em silêncio; aqui ela vai
        // para o shell-menu.log no formato padrão (nunca para a UI).
        try
        {
            return DropCore(containerPath, files);
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop)", 0, "n/a", "n/a", "n/a", $"ex-{ex.GetType().Name}"));
            return false;
        }
    }

    private static bool DropCore(string containerPath, List<string> files)
    {
        // Drag-and-drop exige OleInitialize (não basta CoInitialize da STA) —
        // diretriz da MS ("Transferring Shell Objects with Drag-and-Drop").
        // Handlers (zipfldr etc.) usam APIs OLE internas e recusam (NONE) sem isso.
        // P/Invoke in-box (ole32), sem dependência nova. Escopo = esta operação.
        int oleHr = HDropNative.OleInitialize(IntPtr.Zero);
        if (oleHr != S_OK && oleHr != S_FALSE_OLE_ALREADY_INITIALIZED)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop)", 0, $"0x{(uint)oleHr:X8}", "n/a", "n/a", "ole-init-failed"));
            return false;
        }

        try
        {
            return DropCoreInitialized(containerPath, files);
        }
        finally
        {
            HDropNative.OleUninitialize();
        }
    }

    private static bool DropCoreInitialized(string containerPath, List<string> files)
    {
        ShellBindStage stage = ShellBindHelper.BindDropTarget(
            containerPath, out ShellBindScope bind, out HRESULT hr, out object? dropObj);
        using (bind)
        {
            if (stage != ShellBindStage.Ok || dropObj is not IShellDropTarget dropTarget)
            {
                // Sem DropHandler registrado (ex.: extensão sem app instalado):
                // parse/bind/menu-failed viram false — a F4 assume o fallback.
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop)", 0, $"0x{(uint)hr:X8}", "n/a", "n/a",
                    stage == ShellBindStage.ParseFailed ? "parse-failed"
                    : stage == ShellBindStage.BindFailed ? "bind-failed" : "no-drop-handler"));
                return false;
            }

            // IDataObject do próprio Shell (o mesmo que o Explorer entrega):
            // o alvo pode exigir formatos além do HDROP (SHELLIDLIST etc.).
            // Exige todas as origens na mesma pasta (caso real: Desktop);
            // fora disso, cai no HDrop manual.
            var extraScopes = new List<ShellBindScope>();
            try
            {
                object? shellData = BuildShellDataObject(files, extraScopes);
                object dataObj = shellData ?? (object)new HDropDataObject(files);

                var pt = new POINTL();
                uint effect = DROPEFFECT_COPY;

                int enterHr = dropTarget.DragEnter(dataObj, MK_LBUTTON, pt, ref effect);
                if (enterHr != S_OK || (effect & DROPEFFECT_COPY) == 0)
                {
                    ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                        containerPath, "(drop)", 0, $"0x{(uint)enterHr:X8}", "n/a", "n/a", "effect-refused"));
                    return false;
                }

                effect = DROPEFFECT_COPY;
                int dropHr = dropTarget.Drop(dataObj, MK_LBUTTON, pt, ref effect);
                bool ok = dropHr == S_OK;
                if (ok)
                {
                    // O DropTarget do zip grava em thread própria (SHCreateThread
                    // sem handle — ver blog de referência): host one-shot que sai
                    // na hora mata o worker e nada é gravado. Segura a saída
                    // até o arquivo estabilizar (best-effort, teto próprio).
                    // Baseline pós-Drop: escrita síncrona já aparece aqui.
                    (long settledLen, long settledTicks) = ReadArchiveStamp(containerPath);
                    WaitForArchiveSettled(containerPath, settledLen, settledTicks);
                }

                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop)", (uint)files.Count, $"0x{(uint)enterHr:X8}", "n/a", $"0x{(uint)dropHr:X8}",
                    ok ? "ok" : "drop-failed"));
                return ok;
            }
            finally
            {
                foreach (var scope in extraScopes)
                {
                    try
                    {
                        scope.Dispose();
                    }
                    catch
                    {
                        // Dispose best-effort.
                    }
                }
            }
        }
    }

    // Monta o IDataObject das origens via o próprio Shell (pai comum +
    // GetUIObjectOf IID_IDataObject) — fidelidade total ao Explorer.
    // Devolve null se as origens estiverem em pastas distintas ou o bind
    // falhar (o chamador usa o HDrop manual). Scopes ficam vivos até o Drop.
    private static object? BuildShellDataObject(List<string> files, List<ShellBindScope> scopes)
    {
        try
        {
            string? dir = Path.GetDirectoryName(files[0]);
            foreach (string f in files)
            {
                if (!string.Equals(Path.GetDirectoryName(f), dir, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            var childRels = new List<IntPtr>(files.Count);
            IShellFolder? folder = null;
            foreach (string f in files)
            {
                ShellBindStage fileStage = ShellBindHelper.ParseAndBindParent(
                    f, out ShellBindScope scope, out HRESULT fileHr, out IntPtr childRel);
                if (fileStage != ShellBindStage.Ok || fileHr.Failed)
                {
                    return null;
                }

                scopes.Add(scope);
                folder ??= scope.Folder;
                childRels.Add(childRel);
            }

            if (folder is null || childRels.Count == 0)
            {
                return null;
            }

            Guid iidDataObject = new("0000010E-0000-0000-C000-000000000046");
            HRESULT menuHr = folder.GetUIObjectOf(
                Vanara.PInvoke.HWND.NULL, (uint)childRels.Count, childRels.ToArray(),
                in iidDataObject, IntPtr.Zero, out object? dataObj);
            if (menuHr.Failed || dataObj is null)
            {
                return null;
            }

            return dataObj;
        }
        catch
        {
            return null;
        }
    }

    // Carimbo atual do arquivo (-1/-1 se ausente/travado): baseline do settle.
    private static (long Length, long Ticks) ReadArchiveStamp(string containerPath)
    {
        try
        {
            var info = new FileInfo(containerPath);
            if (!info.Exists)
            {
                return (-1, -1);
            }

            return (info.Length, info.LastWriteTimeUtc.Ticks);
        }
        catch
        {
            return (-1, -1);
        }
    }

    // Segura o host one-shot até a gravação assíncrona do handler terminar:
    // o worker pode largar devagar — espera a PRIMEIRA mudança em relação ao
    // baseline (teto 30s) e depois estabilidade de 1s (presença mínima total
    // 1,5s, teto geral 60s). Best-effort: nunca falha o drop por isso.
    private static void WaitForArchiveSettled(string containerPath, long baseLen, long baseTicks)
    {
        const int pollMs = 250;
        const int minPolls = 6;
        const int stablePolls = 4;
        const int maxPollsNoChange = 120;
        const int maxPolls = 240;

        long lastLen = -1;
        long lastTicks = -1;
        int stable = 0;
        bool seenChange = false;
        try
        {
            for (int i = 0; i < maxPolls; i++)
            {
                System.Threading.Thread.Sleep(pollMs);
                long len;
                long ticks;
                try
                {
                    var info = new FileInfo(containerPath);
                    if (!info.Exists)
                    {
                        continue;
                    }

                    len = info.Length;
                    ticks = info.LastWriteTimeUtc.Ticks;
                }
                catch
                {
                    // Arquivo travado pela gravação = worker vivo: continua.
                    stable = 0;
                    seenChange = true;
                    continue;
                }

                if (len == lastLen && ticks == lastTicks)
                {
                    stable++;
                }
                else
                {
                    if (len != baseLen || ticks != baseTicks)
                    {
                        seenChange = true;
                    }

                    stable = 0;
                    lastLen = len;
                    lastTicks = ticks;
                }

                if (seenChange && i + 1 >= minPolls && stable >= stablePolls)
                {
                    return;
                }

                if (!seenChange && i + 1 >= maxPollsNoChange)
                {
                    return;
                }
            }
        }
        catch
        {
            // Sleep interrompido: sai mesmo assim.
        }
    }

    // IDropTarget (oleidl.h, IID 00000122-...). Só DragEnter/Drop são usados
    // (simulação direta, sem loop de DragOver — molde do blog de referência);
    // PreserveSig para ler o HRESULT sem exceção.
    [ComImport]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellDropTarget
    {
        [PreserveSig]
        int DragEnter(
            [MarshalAs(UnmanagedType.Interface)] object pDataObj,
            uint grfKeyState,
            POINTL pt,
            ref uint pdwEffect);

        [PreserveSig]
        int DragOver(uint grfKeyState, POINTL pt, ref uint pdwEffect);

        [PreserveSig]
        int DragLeave();

        [PreserveSig]
        int Drop(
            [MarshalAs(UnmanagedType.Interface)] object pDataObj,
            uint grfKeyState,
            POINTL pt,
            ref uint pdwEffect);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTL
    {
        public int X;
        public int Y;
    }

    // IDataObject mínimo com CF_HDROP (DROPFILES Unicode): o DropTarget do
    // container só pede formato < CF_MAX (15 = CF_HDROP); resto = E_NOTIMPL.
    // Cada GetData aloca um HGLOBAL novo — o chamador libera via
    // ReleaseStgMedium (pUnkForRelease null = dono é o recebedor).
    [ComVisible(true)]
    private sealed class HDropDataObject : ComTypes.IDataObject
    {
        private readonly string[] _files;

        public HDropDataObject(IReadOnlyList<string> files)
        {
            _files = files.ToArray();
        }

        public void GetData(ref ComTypes.FORMATETC format, out ComTypes.STGMEDIUM medium)
        {
            medium = new ComTypes.STGMEDIUM();
            if (format.cfFormat != CF_HDROP
                || (format.tymed & ComTypes.TYMED.TYMED_HGLOBAL) == 0)
            {
                throw new COMException("formato incompatível com drop de arquivos", DV_E_FORMATETC);
            }

            medium.tymed = ComTypes.TYMED.TYMED_HGLOBAL;
            medium.unionmember = HDropNative.BuildHDrop(_files);
        }

        // Assinaturas do .NET 8 (ComTypes): QueryGetData/Canonical/DAdvise/
        // EnumDAdvise retornam HRESULT int; EnumFormatEtc devolve o enumerador.
        public int QueryGetData(ref ComTypes.FORMATETC format)
        {
            if (format.cfFormat != CF_HDROP
                || (format.tymed & ComTypes.TYMED.TYMED_HGLOBAL) == 0)
            {
                return DV_E_FORMATETC;
            }

            return S_OK;
        }

        public void GetDataHere(ref ComTypes.FORMATETC format, ref ComTypes.STGMEDIUM medium)
            => throw new COMException("não suportado", E_NOTIMPL);

        public int GetCanonicalFormatEtc(ref ComTypes.FORMATETC formatIn, out ComTypes.FORMATETC formatOut)
        {
            formatOut = formatIn;
            return DATA_S_SAMEFORMATETC;
        }

        public void SetData(ref ComTypes.FORMATETC format, ref ComTypes.STGMEDIUM medium, bool release)
            => throw new COMException("somente leitura", E_NOTIMPL);

        public ComTypes.IEnumFORMATETC EnumFormatEtc(ComTypes.DATADIR direction)
            => throw new COMException("não suportado", E_NOTIMPL);

        public int DAdvise(ref ComTypes.FORMATETC format, ComTypes.ADVF advf, ComTypes.IAdviseSink adviseSink, out int connection)
        {
            connection = 0;
            return E_NOTIMPL;
        }

        public void DUnadvise(int connection)
            => throw new COMException("não suportado", E_NOTIMPL);

        public int EnumDAdvise(out ComTypes.IEnumSTATDATA? enumAdvise)
        {
            enumAdvise = null;
            return E_NOTIMPL;
        }
    }

    // DROPFILES (shellapi.h): pFiles + pt + fNC + fWide, tudo 4 bytes.
    // Layout: header de 20 bytes + lista UTF-16 "a\0b\0\0".
    private static class HDropNative
    {
        private const uint GMEM_MOVEABLE = 0x0002;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct DROPFILES
        {
            public uint pFiles;
            public int ptX;
            public int ptY;
            public int fNC;
            public int fWide;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        // Drag-and-drop exige OleInitialize na thread (diretriz MS).
        [DllImport("ole32.dll")]
        internal static extern int OleInitialize(IntPtr pvReserved);

        [DllImport("ole32.dll")]
        internal static extern void OleUninitialize();

        public static IntPtr BuildHDrop(string[] files)
        {
            string joined = string.Join("\0", files) + "\0\0";
            byte[] text = Encoding.Unicode.GetBytes(joined);
            int headerSize = Marshal.SizeOf<DROPFILES>();

            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(headerSize + text.Length));
            if (hGlobal == IntPtr.Zero)
            {
                throw new OutOfMemoryException("GlobalAlloc falhou para o HDROP");
            }

            IntPtr p = GlobalLock(hGlobal);
            if (p == IntPtr.Zero)
            {
                throw new InvalidOperationException("GlobalLock falhou para o HDROP");
            }

            try
            {
                Marshal.StructureToPtr(
                    new DROPFILES { pFiles = (uint)headerSize, fWide = 1 },
                    p,
                    fDeleteOld: false);
                Marshal.Copy(text, 0, p + headerSize, text.Length);
            }
            finally
            {
                GlobalUnlock(p);
            }

            return hGlobal;
        }
    }
}
