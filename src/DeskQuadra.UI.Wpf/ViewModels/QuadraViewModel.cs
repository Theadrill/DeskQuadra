using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;

namespace DeskQuadra.UI.Wpf.ViewModels;

public sealed class QuadraViewModel : ViewModelBase
{
    private readonly Quadra _quadra;

    public Quadra Model => _quadra;

    public Guid Id => _quadra.Id;

    public string Title
    {
        get => _quadra.Title;
        set
        {
            if (_quadra.Title != value)
            {
                _quadra.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public double Left
    {
        get => _quadra.Left;
        set
        {
            if (Math.Abs(_quadra.Left - value) > 0.001)
            {
                _quadra.Left = value;
                OnPropertyChanged();
            }
        }
    }

    public double Top
    {
        get => _quadra.Top;
        set
        {
            if (Math.Abs(_quadra.Top - value) > 0.001)
            {
                _quadra.Top = value;
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get => _quadra.Width;
        set
        {
            if (Math.Abs(_quadra.Width - value) > 0.001)
            {
                _quadra.Width = value;
                OnPropertyChanged();
            }
        }
    }

    public double Height
    {
        get => _quadra.Height;
        set
        {
            if (Math.Abs(_quadra.Height - value) > 0.001)
            {
                _quadra.Height = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDefault => _quadra.IsDefault;

    public bool IsLocked
    {
        get => _quadra.IsLocked;
        set
        {
            if (_quadra.IsLocked != value)
            {
                _quadra.IsLocked = value;
                OnPropertyChanged();
            }
        }
    }

    // Modo roll-up: flag pura com notificação (persistência já existe no modelo/DTO)
    public bool IsCollapsed
    {
        get => _quadra.IsCollapsed;
        set
        {
            if (_quadra.IsCollapsed != value)
            {
                _quadra.IsCollapsed = value;
                OnPropertyChanged();
            }
        }
    }

    public SortMode SortMode
    {
        get => _quadra.SortMode;
        set
        {
            if (_quadra.SortMode != value)
            {
                _quadra.SortMode = value;
                OnPropertyChanged();
                SortItems(value);
            }
        }
    }

    public bool HasItems => Items.Count > 0;

    public Visibility EmptyMessageVisibility => Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<DesktopItemViewModel> Items { get; } = new();

    private readonly IIconExtractorService _iconExtractor;

    public QuadraViewModel(Quadra quadra, IIconExtractorService iconExtractor)
    {
        _quadra = quadra;
        _iconExtractor = iconExtractor;

        foreach (var item in quadra.Items)
        {
            Items.Add(new DesktopItemViewModel(item, iconExtractor));
        }

        if (quadra.SortMode != SortMode.Manual)
        {
            SortItems(quadra.SortMode);
        }
    }

    public void AddItem(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string displayName = Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(filePath)
            : Path.GetFileName(filePath);

        bool isDir = Directory.Exists(filePath);
        DateTime lastModified = isDir
            ? Directory.GetLastWriteTime(filePath)
            : (File.Exists(filePath) ? File.GetLastWriteTime(filePath) : DateTime.Now);

        var newItem = new DesktopItem(
            name: displayName,
            filePath: filePath,
            targetPath: filePath,
            isDirectory: isDir,
            orderIndex: _quadra.Items.Count,
            lastModified: lastModified);

        _quadra.Items.Add(newItem);
        Items.Add(new DesktopItemViewModel(newItem, _iconExtractor));

        if (SortMode != SortMode.Manual)
        {
            SortItems(SortMode);
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyMessageVisibility));
    }

    public void AddItem(DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.OrderIndex = _quadra.Items.Count;
        _quadra.Items.Add(item);
        Items.Add(new DesktopItemViewModel(item, _iconExtractor));

        if (SortMode != SortMode.Manual)
        {
            SortItems(SortMode);
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyMessageVisibility));
    }

    public bool RemoveItem(DesktopItemViewModel item)
    {
        if (item == null)
        {
            return false;
        }

        bool removed = Items.Remove(item);
        if (removed)
        {
            _quadra.Items.Remove(item.Model);
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Model.OrderIndex = i;
            }
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(EmptyMessageVisibility));
        }

        return removed;
    }

    public void SortItems(SortMode mode)
    {
        _quadra.SortMode = mode;

        List<DesktopItemViewModel> sorted = mode switch
        {
            SortMode.Name => Items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SortMode.Type => Items.OrderBy(i => !i.IsDirectory).ThenBy(i => Path.GetExtension(i.FilePath), StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Name).ToList(),
            SortMode.Date => Items.OrderByDescending(i => i.LastModified).ThenBy(i => i.Name).ToList(),
            _ => Items.OrderBy(i => i.Model.OrderIndex).ToList()
        };

        Items.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Model.OrderIndex = i;
            Items.Add(sorted[i]);
        }

        _quadra.Items = Items.Select(vm => vm.Model).ToList();
        OnPropertyChanged(nameof(SortMode));
    }

    public void RefreshItems()
    {
        Items.Clear();
        foreach (var item in _quadra.Items)
        {
            Items.Add(new DesktopItemViewModel(item, _iconExtractor));
        }

        if (SortMode != SortMode.Manual)
        {
            SortItems(SortMode);
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyMessageVisibility));
    }
}
