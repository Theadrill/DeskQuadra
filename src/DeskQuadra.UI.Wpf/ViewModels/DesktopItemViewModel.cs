using System.Windows.Media;
using DeskQuadra.Core.FileSystem;
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

    private bool _isFallbackIcon;

    public ImageSource Icon
    {
        get
        {
            bool fileExists = FileSystemUtils.PathExists(_item.FilePath);
            // Se o ícone ainda não foi carregado OU se era um ícone de fallback e o arquivo voltou a existir no disco:
            if (_icon == null || (_isFallbackIcon && fileExists))
            {
                _isFallbackIcon = !fileExists;
                _icon = _iconExtractor.GetIcon(_item.FilePath, large: true);
            }
            return _icon;
        }
    }

    public void InvalidateIcon()
    {
        _icon = null;
        _isFallbackIcon = false;
        OnPropertyChanged(nameof(Icon));
    }

    private string? _toolTipText;
    public string ToolTipText
    {
        get => _toolTipText ?? _item.Name;
        private set => SetProperty(ref _toolTipText, value);
    }

    public void ResetToolTip()
    {
        ToolTipText = _item.Name;
    }

    public void ShowFullPathInToolTip()
    {
        ToolTipText = !string.IsNullOrWhiteSpace(_item.FilePath) ? _item.FilePath : _item.Name;
    }

    public void UpdateNameAndPath(string newName, string newFilePath)
    {
        _item.Name = newName;
        _item.FilePath = newFilePath;
        _item.TargetPath = newFilePath;
        _icon = null;
        _isFallbackIcon = false;
        _toolTipText = newName;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(ToolTipText));
    }

    public DesktopItemViewModel(DesktopItem item, IIconExtractorService iconExtractor)
    {
        _item = item;
        _iconExtractor = iconExtractor;
    }
}
