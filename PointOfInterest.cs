using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Info;

namespace cycloid;

public partial class PointOfInterest : ObservableObject
{
    private byte _trackMask;
    private int? _onTrackCount;

    [ObservableProperty]
    private string _name;

    public InfoType Type { get; set; }

    public DateTime Created { get; set; }

    public MapPoint Location { get; set; }

    public int? OnTrackCount 
    {
        get => _onTrackCount;
        private set
        {
            SetProperty(ref _onTrackCount, value);
        }
    }

    public byte TrackMask
    {
        get => _trackMask;
        private set
        {
            SetProperty(ref _trackMask, value);
        }
    }

    public bool IsSection => InfoCategory.Section.Types.Contains(Type);

    public void InitOnTrackCount(int value, byte? trackMask = null)
    {
        _onTrackCount = value;
        if (trackMask is byte mask)
        {
            _trackMask = mask;
        }
        else
        {
            _trackMask = 0;
            while (value-- > 0)
            {
                _trackMask <<= 1;
                _trackMask |= 1;
            }
        }

        OnPropertyChanged(nameof(OnTrackCount));
    }

    public void SetTrackMaskBit(int position) => TrackMask |= GetMask(position, false);

    public void ClearTrackMaskBit(int position) => TrackMask &= GetMask(position, true);

    public bool IsTrackMaskBitSet(int position) => (TrackMask & GetMask(position, false)) != 0;

    public bool IsTrackMaskZero() => TrackMask == 0;

    private byte GetMask(int position, bool invert)
    {
        byte mask = 1;
        mask <<= position;

        return invert ? (byte)~mask : mask;
    }
}

