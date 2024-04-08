using CommunityToolkit.Mvvm.Messaging.Messages;

namespace cycloid;

public class SetMapCenterMessage(MapPoint location) : ValueChangedMessage<MapPoint>(location)
{ }
