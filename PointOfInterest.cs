using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Info;

namespace cycloid;

public partial class PointOfInterest : ObservableObject
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public InfoType Type { get; set; }

    public InfoCategory Category { get; set; }

    public DateTime Created { get; set; }

    public MapPoint Location { get; set; }

    public int? OnTrackCount { get; private set; }

    public byte TrackMask { get; private set; }

    public bool IsSection => InfoCategory.Section.Types.Contains(Type);

    public void InitOnTrackCount(int value, byte? trackMask = null)
    {
        OnTrackCount = value;
        if (trackMask is byte mask)
        {
            TrackMask = mask;
        }
        else
        {
            TrackMask = 0;
            while (value-- > 0)
            {
                TrackMask <<= 1;
                TrackMask |= 1;
            }
        }

        OnPropertyChanged(nameof(OnTrackCount));
    }

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

