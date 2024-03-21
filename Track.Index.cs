namespace cycloid;

partial class Track
{
    public readonly record struct Index(int SegmentIndex, int PointIndex)
    {
        public static readonly Index Invalid = new(-1, -1);

        public bool IsValid => this != Invalid;
    }
}
