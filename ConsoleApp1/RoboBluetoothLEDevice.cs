using System;

namespace ConsoleApp1
{
	public class RoboBluetoothLEDevice
	{
		public DateTimeOffset BroadcastTime { get; }
		public ulong Address { get; }
		public string Name { get; }
		public short SignalStrengthInDB { get; }

		public RoboBluetoothLEDevice(DateTimeOffset broadcastTime, ulong address, string name, short signalStrengthInDb)
		{
			BroadcastTime = broadcastTime;
			Address = address;
			Name = name;
			SignalStrengthInDB = signalStrengthInDb;
		}

		public override string ToString()
		{
			return $"{(string.IsNullOrEmpty(Name) ? "[No Name]" : Name)} {Address} ({SignalStrengthInDB})";
		}
	}
}