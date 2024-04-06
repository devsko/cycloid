using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cycloid.Info;

public partial class InfoCache : ObservableObject
{
    private record struct BucketPoint(int Bottom, int Left);
    private record struct InfoBucket(BucketPoint Point, InfoPoint[] Infos);

    private const float BucketWidth = .1f;
    private static readonly MapPoint BucketSize = new(BucketWidth, BucketWidth);

    private readonly OsmClient _client = new();
    private readonly Dictionary<BucketPoint, InfoBucket> _buckets = [];
    private readonly List<InfoBucket> _activated = new(9);

    [ObservableProperty]
    private int _cachedCount;

    [ObservableProperty]
    private int _activatedCount;

    public event Action<InfoPoint[]> InfosActivated;
    public event Action<int, int> InfosDeactivated;

    public async Task SetCenterAsync(MapPoint point, CancellationToken cancellationToken)
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
            _buckets.Add(point, bucket);
            CachedCount += bucket.Infos.Length;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _activated.Add(bucket);

        InfosActivated?.Invoke(bucket.Infos);
        ActivatedCount += bucket.Infos.Length;
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

        InfosDeactivated?.Invoke(startIndex, bucket.Infos.Length);
        ActivatedCount -= bucket.Infos.Length;
    }

    private async Task<InfoBucket> LoadAsync(BucketPoint point)
    {
        OverpassPoint[] overpassPoints = await _client.GetPointsAsync(new MapPoint((float)point.Bottom * BucketWidth, (float)point.Left * BucketWidth), BucketSize, default).ConfigureAwait(false);
        InfoPoint[] infos = new InfoPoint[overpassPoints.Length];
        for (int i = 0; i < overpassPoints.Length; i++)
        {
            infos[i] = InfoPoint.FromOverpassPoint(overpassPoints[i]);
        }

        return new InfoBucket { Point = point, Infos = infos };
    }
}