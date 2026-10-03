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

    public DateTime LastModified => _item.LastModified;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    private string _editName = string.Empty;
    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    private bool _isDropTarget;
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

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

    public void UpdateNameAndPath(string newName, string newFilePath)
    {
        _item.Name = newName;
        _item.FilePath = newFilePath;
        _item.TargetPath = newFilePath;
        _icon = null;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Icon));
    }

    public DesktopItemViewModel(DesktopItem item, IIconExtractorService iconExtractor)
    {
        _item = item;
        _iconExtractor = iconExtractor;
    }
}
