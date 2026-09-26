using DeskQuadra.Core.Models;

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

    public QuadraViewModel(Quadra quadra)
    {
        _quadra = quadra;
    }
}
