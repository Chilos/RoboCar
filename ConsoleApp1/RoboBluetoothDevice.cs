namespace ConsoleApp1
{
	public class RoboBluetoothDevice
	{
		public string Id { get; }
		public string Name { get; }
		public ulong Address { get; }

		public RoboBluetoothDevice(string name, ulong address, string id)
		{
			Name = name;
			Address = address;
			Id = id;
		}
		public override string ToString()
		{
			return $"{(string.IsNullOrEmpty(Name) ? "[No Name]" : Name)} {Address}";
		}
	}
}