using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Bluetooth.Advertisement;

namespace ConsoleApp1
{
	public class RoboBluetoothLEAdvertisementWatcher
	{
		private readonly BluetoothLEAdvertisementWatcher _watcher;

		private readonly Dictionary<ulong, RoboBluetoothLEDevice> _discoverDevices =
			new Dictionary<ulong, RoboBluetoothLEDevice>();

		private readonly object _threadLock = new object();

		public bool Listening => _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;

		public IReadOnlyCollection<RoboBluetoothLEDevice> DiscoverDevices
		{
			get
			{
				CleanupTimeouts();
				lock (_threadLock)
				{
					return _discoverDevices.Values.ToList().AsReadOnly();
				}
			}
		}

		public int HeartbeatTimeout { get; set; } = 30;

		public event Action StoppedListening = () => { };
		public event Action StartedListening = () => { };
		public event Action<RoboBluetoothLEDevice> NewDeviceDiscovered = (device) => { };
		public event Action<RoboBluetoothLEDevice> DeviceDiscovered = (device) => { };
		public event Action<RoboBluetoothLEDevice> DeviceNameChanged = (device) => { };
		public event Action<RoboBluetoothLEDevice> DeviceTimeout = (device) => { };


		public RoboBluetoothLEAdvertisementWatcher()
		{
			_watcher = new BluetoothLEAdvertisementWatcher
			{
				ScanningMode = BluetoothLEScanningMode.Active,
				
			};
			_watcher.Received += async (sender, args) =>
			{
				CleanupTimeouts();
				//var info = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
				RoboBluetoothLEDevice device = null;
				var newDiscovery = !_discoverDevices.ContainsKey(args.BluetoothAddress);
				var nameChanged = !newDiscovery
				                  && !string.IsNullOrEmpty(args.Advertisement.LocalName)
				                  && _discoverDevices[args.BluetoothAddress].Name != args.Advertisement.LocalName;
				lock (_threadLock)
				{
					var name = args.Advertisement.LocalName;
					if (string.IsNullOrEmpty(name) && !newDiscovery)
						name = _discoverDevices[args.BluetoothAddress].Name;
					device = new RoboBluetoothLEDevice
					(
						args.Timestamp,
						args.BluetoothAddress,
						name,
						args.RawSignalStrengthInDBm
					);
					_discoverDevices[args.BluetoothAddress] = device;
				}
				DeviceDiscovered(device);
				if (nameChanged)
					DeviceNameChanged(device);
				if (newDiscovery)
					NewDeviceDiscovered(device);
			};

			_watcher.Stopped += (sender, args) => { StoppedListening(); };
		}

		public void StartListening()
		{
			if (Listening) return;
			_watcher.Start();
			StartedListening();
		}

		public void StopListening()
		{
			if (!Listening) return;
			_watcher.Stop();
			lock (_threadLock)
			{
				_discoverDevices.Clear();
			}
			StoppedListening();
		}

		private void CleanupTimeouts()
		{
			lock (_threadLock)
			{
				var threshold = DateTime.Now - TimeSpan.FromSeconds(HeartbeatTimeout);
				_discoverDevices.Where(f => f.Value.BroadcastTime < threshold).ToList().ForEach(device =>
				{
					_discoverDevices.Remove(device.Key);
					DeviceTimeout(device.Value);
				});
			}
		}
	}
}