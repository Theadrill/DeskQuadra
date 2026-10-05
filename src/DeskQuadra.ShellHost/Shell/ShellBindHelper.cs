using System.Runtime.InteropServices;
using Vanara;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.ShellHost.Shell;

// F1 3ª auditoria: preâmbulo COM único (SHParseDisplayName → SHBindToParent →
// IShellFolder.GetUIObjectOf) copiado 3x (query + 2 invokes). Centraliza SÓ o
// bind + dispose pattern (ordem original: menu, folder, pidl). SEM logs aqui:
// cada chamador registra com seu formato (FormatQueryFailed vs FormatInvoke),
// com as mesmas mensagens de antes.
internal enum ShellBindStage
{
    Ok,
    ParseFailed,
    BindFailed,
    MenuFailed,
}

// Dono do lifetime do bind: PIDL + RCWs folder/menu. Dispose libera na ordem
// original (menu → folder → pidl) e é no-op seguro p/ estágios falhos.
// No caminho de FUNDO há um RCW a mais (a pasta Desktop, pai do bind) —
// liberado após o folder (menu → folder → desktop → pidl); nulo no caminho
// de item (no-op).
internal sealed class ShellBindScope : IDisposable
{
    internal PIDL? Pidl;
    internal IShellFolder? Folder;
    internal IShellFolder? DesktopFolder;
    internal IntPtr ChildRel;
    internal IContextMenu? ContextMenu;
    private object? _folderObj;
    private object? _menuObj;
    private bool _disposed;

    internal void SetFolder(object? folderObj, IShellFolder? folder, IntPtr childRel)
    {
        _folderObj = folderObj;
        Folder = folder;
        ChildRel = childRel;
    }

    internal void SetMenu(object? menuObj, IContextMenu? contextMenu)
    {
        _menuObj = menuObj;
        ContextMenu = contextMenu;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_menuObj is not null)
        {
            Marshal.ReleaseComObject(_menuObj);
        }

        // Folder e _folderObj são o mesmo RCW: liberar UMA vez (o código
        // original liberava `folder`, o IShellFolder).
        if (Folder is not null)
        {
            Marshal.ReleaseComObject(Folder);
        }

        // Só o caminho de fundo preenche (RCW próprio do SHGetDesktopFolder).
        if (DesktopFolder is not null)
        {
            Marshal.ReleaseComObject(DesktopFolder);
        }

        if (Pidl is not null && !Pidl.IsNull)
        {
            Pidl.Dispose();
        }
    }
}

internal static class ShellBindHelper
{
    // Despacho único item/fundo (query + 2 invokes usam este ponto): fundo
    // binda o IContextMenu da própria pasta (verbos de FUNDO), item mantém o
    // GetUIObjectOf de antes (verbos de item, intacto).
    internal static ShellBindStage BindFor(string path, bool background, out ShellBindScope scope, out HRESULT hr)
        => background
            ? BindBackgroundContextMenu(path, out scope, out hr)
            : BindContextMenu(path, out scope, out hr);

    // Caminho de FUNDO da pasta (espaço vazio/barra): SHParseDisplayName da
    // pasta → bind ao IShellFolder da PRÓPRIA pasta → CreateViewObject
    // IID_IContextMenu (o objeto da pasta em si, não de um item dentro dela).
    // É o caminho documentado do menu de fundo do Explorer (a DefView usa o
    // IContextMenu do folder via CreateViewObject); GetUIObjectOf com o PIDL
    // da pasta devolveria os verbos DA PASTA COMO ITEM (o bug: "Enviar para",
    // "Transmitir para Dispositivo"). APIs conferidas na Vanara 5.0.7
    // referenciada (SHGetDesktopFolder + IShellFolder.BindToObject +
    // IShellFolder.CreateViewObject) — sem nova dependência. Mesmos estágios
    // e contrato do BindContextMenu (nunca lança por HRESULT; exceção de COM
    // propaga p/ o try/catch do chamador; logs no chamador, inalterados).
    internal static ShellBindStage BindBackgroundContextMenu(string path, out ShellBindScope scope, out HRESULT hr)
    {
        scope = new ShellBindScope();

        hr = SHParseDisplayName(path, null, out PIDL pidl, 0, out _);
        scope.Pidl = pidl;
        if (hr.Failed || pidl.IsNull)
        {
            return ShellBindStage.ParseFailed;
        }

        hr = SHGetDesktopFolder(out IShellFolder? desktop);
        scope.DesktopFolder = desktop;
        if (hr.Failed || desktop is null)
        {
            return ShellBindStage.BindFailed;
        }

        Guid iidFolder = typeof(IShellFolder).GUID;
        hr = desktop.BindToObject(pidl, null, in iidFolder, out object? folderObj);
        IShellFolder? folder = folderObj as IShellFolder;
        scope.SetFolder(folderObj, folder, IntPtr.Zero);
        if (hr.Failed || folder is null)
        {
            return ShellBindStage.BindFailed;
        }

        Guid iidMenu = typeof(IContextMenu).GUID;
        hr = folder.CreateViewObject(HWND.NULL, in iidMenu, out object? menuObj);
        IContextMenu? contextMenu = menuObj as IContextMenu;
        scope.SetMenu(menuObj, contextMenu);
        if (hr.Failed || contextMenu is null)
        {
            return ShellBindStage.MenuFailed;
        }

        return ShellBindStage.Ok;
    }

    // Equivalente literal aos 3 preâmbulos: .lnk = bind sobre o próprio link
    // (SEM resolver o alvo antes). Nunca lança por HRESULT — devolve o estágio
    // + hr p/ o chamador logar no seu formato. Exceção de COM propaga como
    // antes (o chamador mantém seu try/catch + using dá o dispose).
    internal static ShellBindStage BindContextMenu(string path, out ShellBindScope scope, out HRESULT hr)
    {
        ShellBindStage stage = ParseAndBindParent(path, out scope, out hr, out IntPtr childRel);
        if (stage != ShellBindStage.Ok)
        {
            return stage;
        }

        Guid iidMenu = typeof(IContextMenu).GUID;
        hr = scope.Folder!.GetUIObjectOf(HWND.NULL, 1, new[] { childRel }, in iidMenu, IntPtr.Zero, out object? menuObj);
        IContextMenu? contextMenu = menuObj as IContextMenu;
        scope.SetMenu(menuObj, contextMenu);
        if (hr.Failed || contextMenu is null)
        {
            return ShellBindStage.MenuFailed;
        }

        return ShellBindStage.Ok;
    }

    // F3 drop-em-container: mesmo preâmbulo do menu (parse + bind do pai),
    // mas resolve IID_IDropTarget em vez de IContextMenu. O Shell exige
    // cidl == 1 para IDropTarget — um container por chamada (o chamador itera).
    // .lnk = bind sobre o próprio link (mesma regra do menu, §1).
    internal static ShellBindStage BindDropTarget(
        string path, out ShellBindScope scope, out HRESULT hr, out object? dropTargetObj)
    {
        dropTargetObj = null;
        ShellBindStage stage = ParseAndBindParent(path, out scope, out hr, out IntPtr childRel);
        if (stage != ShellBindStage.Ok)
        {
            return stage;
        }

        Guid iidDropTarget = new("00000122-0000-0000-C000-000000000046");
        hr = scope.Folder!.GetUIObjectOf(HWND.NULL, 1, new[] { childRel }, in iidDropTarget, IntPtr.Zero, out dropTargetObj);
        if (hr.Failed || dropTargetObj is null)
        {
            return ShellBindStage.MenuFailed;
        }

        return ShellBindStage.Ok;
    }

    // Preâmbulo compartilhado (F1 3ª auditoria, estendido na F3): parse do
    // caminho + bind da pasta pai. Extração na 2ª repetição (menu + drop) —
    // comportamento idêntico, ordem de dispose inalterada no scope.
    // Internal (não private) porque o IDataObject das origens (F3) usa o
    // mesmo preâmbulo — 3º consumidor da mesma forma, sem copiar.
    internal static ShellBindStage ParseAndBindParent(
        string path, out ShellBindScope scope, out HRESULT hr, out IntPtr childRel)
    {
        scope = new ShellBindScope();
        childRel = IntPtr.Zero;

        hr = SHParseDisplayName(path, null, out PIDL pidl, 0, out _);
        scope.Pidl = pidl;
        if (hr.Failed || pidl.IsNull)
        {
            return ShellBindStage.ParseFailed;
        }

        hr = SHBindToParent(pidl, typeof(IShellFolder).GUID, out object? folderObj, out childRel);
        IShellFolder? folder = folderObj as IShellFolder;
        scope.SetFolder(folderObj, folder, childRel);
        if (hr.Failed || folder is null || childRel == IntPtr.Zero)
        {
            return ShellBindStage.BindFailed;
        }

        return ShellBindStage.Ok;
    }
}
