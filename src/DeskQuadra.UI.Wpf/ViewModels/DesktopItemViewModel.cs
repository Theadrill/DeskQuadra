using System.Windows.Media;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;

namespace DeskQuadra.UI.Wpf.ViewModels;

/// <summary>
/// ViewModel de apresentação de um item ou atalho individual na grade da Quadra.
/// </summary>
public sealed class DesktopItemViewModel : ViewModelBase
{
    private readonly DesktopItem _item;
    private readonly IIconExtractorService _iconExtractor;
    private ImageSource? _icon;

    public DesktopItem Model => _item;

    public Guid Id => _item.Id;

    public string Name => _item.Name;

    public string FilePath => _item.FilePath;

    public bool IsDirectory => _item.IsDirectory;

    public ImageSource Icon
    {
        get
        {
            if (_icon == null)
            {
                _icon = _iconExtractor.GetIcon(_item.FilePath, large: true);
            }
            return _icon;
        }
    }

    public DesktopItemViewModel(DesktopItem item, IIconExtractorService iconExtractor)
    {
        _item = item;
        _iconExtractor = iconExtractor;
    }
}
