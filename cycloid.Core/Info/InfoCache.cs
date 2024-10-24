using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace cycloid.Info;

public class InfosActivated(InfoPoint[] infos)
{
    public InfoPoint[] Infos => infos;
}

public class InfosDeactivated(int index, int count)
{
    public int Index => index;
    public int Count => count;
}

public partial class InfoCache : ObservableObject
{
    private record struct BucketPoint(int Bottom, int Left);
    private record struct InfoBucket(BucketPoint Point, InfoPoint[] Infos);

    private const float BucketWidth = .1f;
    private static readonly MapPoint BucketSize = new(BucketWidth, BucketWidth);

    private readonly OsmClient _client = new();
    private readonly Dictionary<BucketPoint, InfoBucket> _buckets = [];
    private readonly List<InfoBucket> _activated = new(9);

    private int _cachedCount;
    private int _activatedCount;

    public int CachedCount
    {
        get => _cachedCount;
        set => SetProperty(ref _cachedCount, value);
}

    public int ActivatedCount
    {
        get => _activatedCount;
        set => SetProperty(ref _activatedCount, value);
    }

    public async Task LoadAsync(MapPoint point, CancellationToken cancellationToken)
    {
        BucketPoint center = new((int)Math.Floor(point.Latitude / BucketWidth ), (int)Math.Floor(point.Longitude / BucketWidth));
        
        HashSet<BucketPoint> toActivate = [
            new BucketPoint(center.Bottom, center.Left),
            new BucketPoint(center.Bottom - 1, center.Left),
            new BucketPoint(center.Bottom, center.Left - 1),
            new BucketPoint(center.Bottom + 1, center.Left),
            new BucketPoint(center.Bottom, center.Left + 1),
            new BucketPoint(center.Bottom - 1, center.Left - 1),
            new BucketPoint(center.Bottom + 1, center.Left + 1),
            new BucketPoint(center.Bottom - 1, center.Left + 1),
            new BucketPoint(center.Bottom + 1, center.Left - 1),
            ];

        List<InfoBucket> toDeactivate = new(9);
        foreach (InfoBucket activated in _activated)
        {
            if (!toActivate.Remove(activated.Point))
            {
                toDeactivate.Add(activated);
            }
        }

        foreach (InfoBucket bucket in toDeactivate)
        {
            Deactivate(bucket);
        }

        await Task.WhenAll(toActivate.Select(bucketPoint => ActivateAsync(bucketPoint, cancellationToken)));
    }

    private async Task ActivateAsync(BucketPoint point, CancellationToken cancellationToken)
    {
        if (!_buckets.TryGetValue(point, out InfoBucket bucket))
        {
            bucket = await LoadAsync(point);
            if (bucket.Infos is not null)
            {
                _buckets.Add(point, bucket);
                CachedCount += bucket.Infos.Length;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        _activated.Add(bucket);

        InfoPoint[] infos = bucket.Infos ?? [];

        StrongReferenceMessenger.Default.Send(new InfosActivated(infos));
        ActivatedCount += infos.Length;
        
        async Task<InfoBucket> LoadAsync(BucketPoint point)
        {
            OverpassPoint[] overpassPoints = await _client.GetPointsAsync(new MapPoint((float)point.Bottom * BucketWidth, (float)point.Left * BucketWidth), BucketSize, default).ConfigureAwait(false);
            InfoPoint[] infos = overpassPoints.Select(InfoPoint.FromOverpassPoint).ToArray();

            return new InfoBucket { Point = point, Infos = infos };
        }
    }

    private void Deactivate(InfoBucket bucket)
    {
        int bucketIndex = _activated.IndexOf(bucket);
        int startIndex = 0;
        for (int i = 0; i < bucketIndex; i++)
        {
            startIndex += _activated[i].Infos.Length;
        }
        _activated.RemoveAt(bucketIndex);

        StrongReferenceMessenger.Default.Send(new InfosDeactivated(startIndex, bucket.Infos.Length));

        ActivatedCount -= bucket.Infos.Length;
    }
}