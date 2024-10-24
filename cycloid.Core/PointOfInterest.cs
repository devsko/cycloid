using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Info;

namespace cycloid;

public partial class PointOfInterest : ObservableObject
{
    public string Name
    {
        get => field;
        set => SetProperty(ref field, value);
    } = null!; // 'required' not possible because created from Xaml

    public InfoType Type { get; set; }

    public InfoCategory Category { get; set; } = null!; // 'required' not possible because created from Xaml

    public DateTime Created { get; set; }

    public MapPoint Location { get; set; }

    public int? OnTrackCount { get; private set; }

    public byte TrackMask { get; private set; }

    public bool IsSection => InfoCategory.Section.Types.Contains(Type);

    public void InitOnTrackCount(int value, int? trackMask = null)
    {
        OnTrackCount = value;
        if (trackMask is int mask)
        {
            TrackMask = (byte)mask;
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

    private static byte GetMask(int position, bool invert)
    {
        byte mask = 1;
        mask <<= position;

        return invert ? (byte)~mask : mask;
    }
}

