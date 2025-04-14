using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cycloid;

public partial class TrackSplit(float position) : ObservableObject
{
    [ObservableProperty]
    public partial float Position { get; set; } = position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    public partial float DistanceToNext { get; set; }

    public bool CanNotMove { get; init; }

    public bool IsValid => DistanceToNext is >= 90 and < 200;
}

partial class Track
{
    public class SplitCollection : ObservableCollection<TrackSplit>
    {
        private readonly Track _track;

        public SplitCollection(Track track)
        {
            _track = track;
        }

        public void Initialize()
        {
            Add(new TrackSplit(0) { CanNotMove = true });
            TrackSplit last = new(_track.Points.Total.Distance / 1_000) { CanNotMove = true };
            Add(last);
            _track.Points.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PointCollection.Total))
                {
                    last.Position = _track.Points.Total.Distance / 1_000;
                }
            };
        }

        protected override void InsertItem(int index, TrackSplit item)
        {
            int i = index;
            while (i > 0 && this[i - 1].Position > item.Position)
            {
                i--;
            }

            if (i == index)
            {
                while (i < Count && this[i].Position < item.Position)
                {
                    i++;
                }
            }

            base.InsertItem(i, item);
            if (i > 0)
            {
                this[i - 1].DistanceToNext = item.Position - this[i - 1].Position;
            }
            if (i < Count - 1)
            {
                item.DistanceToNext = this[i + 1].Position - item.Position;
            }

            item.PropertyChanged += Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is TrackSplit item && e.PropertyName == nameof(TrackSplit.Position))
            {
                int index = IndexOf(item);
                int i = index;
                while (i >= 1 && this[i - 1].Position > item.Position)
                {
                    i--;
                }

                if (i == index)
                {
                    while (i < Count - 1 && this[i + 1].Position < item.Position)
                    {
                        i++;
                    }
                }

                if (i != index)
                {
                    MoveItem(index, i);
                }
                if (i > 0)
                {
                    this[i - 1].DistanceToNext = item.Position - this[i - 1].Position;
                }
                if (i < Count - 1)
                {
                    item.DistanceToNext = this[i + 1].Position - item.Position;
                }
            }
        }
    }
}
