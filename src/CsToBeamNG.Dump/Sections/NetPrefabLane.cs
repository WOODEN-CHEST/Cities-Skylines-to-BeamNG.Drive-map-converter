namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// A lane of a network prefab, with its settings as the game stores them in <c>NetInfo.Lane</c> (docs/dump-format.md,
/// <c>net-prefabs</c>; docs/research/cs1-networks.md, Lanes). Enumerations are stored as their raw values; the
/// constants are the values the converter uses.
/// </summary>
public sealed class NetPrefabLane
{
    /// <summary><see cref="Direction"/>: traffic goes from the segment's start towards its end (<c>NetInfo.Direction.Forward</c>).</summary>
    public const byte ForwardDirection = 0x1;

    /// <summary><see cref="Direction"/>: traffic goes from the segment's end towards its start (<c>Backward</c>).</summary>
    public const byte BackwardDirection = 0x2;

    /// <summary><see cref="Direction"/>: traffic goes both ways (<c>Both</c>), such as on pedestrian lanes.</summary>
    public const byte BothDirections = 0x3;

    /// <summary><see cref="LaneType"/>: a lane for vehicles (<c>NetInfo.LaneType.Vehicle</c>).</summary>
    public const byte VehicleLane = 0x1;

    /// <summary><see cref="LaneType"/>: a lane for pedestrians (<c>Pedestrian</c>).</summary>
    public const byte PedestrianLane = 0x2;

    /// <summary><see cref="LaneType"/>: parking spaces (<c>Parking</c>).</summary>
    public const byte ParkingLane = 0x4;

    /// <summary><see cref="LaneType"/>: a lane for public transport (<c>PublicTransport</c>), such as a bus lane.</summary>
    public const byte PublicTransportLane = 0x8;

    /// <summary><see cref="LaneType"/>: a lane for cargo vehicles (<c>CargoVehicle</c>), such as on cargo train tracks.</summary>
    public const byte CargoVehicleLane = 0x10;

    /// <summary><see cref="LaneType"/>: a lane for transport vehicles (<c>TransportVehicle</c>), such as buses and trams.</summary>
    public const byte TransportVehicleLane = 0x20;

    /// <summary><see cref="VehicleType"/>: cars, buses and trucks (<c>VehicleInfo.VehicleType.Car</c>).</summary>
    public const uint CarVehicles = 0x1;

    /// <summary><see cref="VehicleType"/>: metro trains (<c>Metro</c>).</summary>
    public const uint MetroVehicles = 0x2;

    /// <summary><see cref="VehicleType"/>: trains (<c>Train</c>).</summary>
    public const uint TrainVehicles = 0x4;

    /// <summary><see cref="VehicleType"/>: bicycles (<c>Bicycle</c>).</summary>
    public const uint BicycleVehicles = 0x20;

    /// <summary><see cref="VehicleType"/>: trams (<c>Tram</c>).</summary>
    public const uint TramVehicles = 0x40;

    /// <summary><see cref="VehicleType"/>: monorail trains (<c>Monorail</c>).</summary>
    public const uint MonorailVehicles = 0x800;

    /// <summary><see cref="VehicleType"/>: cable cars (<c>CableCar</c>).</summary>
    public const uint CableCarVehicles = 0x1000;

    /// <summary>The lane's centre across the segment, in metres from the segment's centre line; negative is left (<c>m_position</c>).</summary>
    public float Position { get; set; }

    /// <summary>The lane's width, in metres (<c>m_width</c>).</summary>
    public float Width { get; set; }

    /// <summary>The lane's height above the segment's centre line, in metres (<c>m_verticalOffset</c>).</summary>
    public float VerticalOffset { get; set; }

    /// <summary>How far from the lane's centre vehicles stop, in metres (<c>m_stopOffset</c>).</summary>
    public float StopOffset { get; set; }

    /// <summary>The speed limit in the game's units (<c>m_speedLimit</c>); 1 is 50 km/h.</summary>
    public float SpeedLimit { get; set; }

    /// <summary>The lane's direction as the prefab defines it, the raw value of <c>NetInfo.Direction</c> (<c>m_direction</c>).</summary>
    public byte Direction { get; set; }

    /// <summary>
    /// The direction the game uses (<c>m_finalDirection</c>): <see cref="Direction"/>, reversed for vehicle and parking
    /// lanes in cities that drive on the left.
    /// </summary>
    public byte FinalDirection { get; set; }

    /// <summary>The lane's type, the raw value of <c>NetInfo.LaneType</c> (<c>m_laneType</c>), a set of bits.</summary>
    public byte LaneType { get; set; }

    /// <summary>The vehicles that may use the lane, the raw value of <c>VehicleInfo.VehicleType</c> (<c>m_vehicleType</c>), a set of bits.</summary>
    public uint VehicleType { get; set; }

    /// <summary>The vehicles that stop on the lane, such as buses at stops, the raw value of <c>VehicleInfo.VehicleType</c> (<c>m_stopType</c>).</summary>
    public uint StopType { get; set; }
}
