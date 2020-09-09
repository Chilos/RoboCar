using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace ConsoleApp1
{
	public class RoboBluetoothWatcher
	{
		private readonly DeviceWatcher _watcher;
		private readonly object _threadLock = new object();
		private readonly Dictionary<ulong, RoboBluetoothDevice> _discoverDevices =
			new Dictionary<ulong, RoboBluetoothDevice>();

		private DataWriter _writer;
		private DataReader _reader;
		private RfcommDeviceService _connectService;
		private StreamSocket _socket;
        
		public bool Listening => _watcher.Status == DeviceWatcherStatus.Started;
		public bool DeviceConnecting => _writer != null;
		public IReadOnlyCollection<RoboBluetoothDevice> DiscoverDevices
		{
			get
			{
				lock (_threadLock)
				{
					return _discoverDevices.Values.ToList().AsReadOnly();
				}
			}
		}
		
		public event Action StoppedListening = () => { };
		public event Action StartedListening = () => { };
		public event Action<RoboBluetoothDevice> NewDeviceDiscovered = (device) => { };
		public event Action<RoboBluetoothDevice> DeviceDiscovered = (device) => { };
		public event Action<RoboBluetoothDevice> DeviceNameChanged = (device) => { };
		public event Action<RoboBluetoothDevice> DeviceTimeout = (device) => { };
		public event Action<RoboBluetoothDevice> DeviceConnected = (device) => { };
		public event Action<string> ReadMessage = message => { };

		public RoboBluetoothWatcher()
		{
			string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
			_watcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")",
				requestedProperties,
				DeviceInformationKind.AssociationEndpoint);
			_watcher.Added += async (sender, args) =>
			{
				var info = await BluetoothDevice.FromIdAsync(args.Id);
				RoboBluetoothDevice device;
				lock (_threadLock)
				{
					device = new RoboBluetoothDevice(info.Name, info.BluetoothAddress, args.Id);
					_discoverDevices[info.BluetoothAddress] = device;
				}
				NewDeviceDiscovered(device);
			};

			_watcher.Updated += async (sender, args) =>
			{
				var info = await BluetoothDevice.FromIdAsync(args.Id);
				RoboBluetoothDevice device;
				var nameChanged = !string.IsNullOrEmpty(info.Name)
				                  && _discoverDevices[info.BluetoothAddress].Name != info.Name;
				lock (_threadLock)
				{
					device = new RoboBluetoothDevice(info.Name, info.BluetoothAddress, args.Id);
					_discoverDevices[info.BluetoothAddress] = device;
				}
				DeviceDiscovered(device);
				if (nameChanged)
					DeviceNameChanged(device);
			};
			_watcher.Removed += async (sender, args) =>
			{
				var info = await BluetoothDevice.FromIdAsync(args.Id);
				RoboBluetoothDevice device;
				lock (_threadLock)
				{
					device = new RoboBluetoothDevice(info.Name, info.BluetoothAddress, args.Id);
					_discoverDevices.Remove(info.BluetoothAddress);
				}
				DeviceTimeout(device);
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

		public async Task ConnectDeviceAsync(string deviceId)
		{
			var bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceId);
			var device = _discoverDevices[bluetoothDevice.BluetoothAddress];
			var rfcommDeviceServicesResult = await bluetoothDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
			_socket = new StreamSocket();
			if (rfcommDeviceServicesResult.Services.Count > 0)
			{ 
				_connectService = rfcommDeviceServicesResult.Services.First();
				await _socket.ConnectAsync(_connectService.ConnectionHostName,
					_connectService.ConnectionServiceName);
				_writer = new DataWriter(_socket.OutputStream);
				_reader = new DataReader(_socket.InputStream);
				_reader.UnicodeEncoding = UnicodeEncoding.Utf8;
				_reader.ByteOrder = ByteOrder.LittleEndian;
				ReceiveStringLoop();
				DeviceConnected(device);
			}
		}

		public void DisconnectDevice()
		{
			if (_writer != null)
			{
				_writer.DetachStream();
				_writer = null;
			}

			if (_connectService != null)
			{
				_connectService.Dispose();
				_connectService = null;
			}

			lock (this)
			{
				if (_socket != null)
				{
					_socket.Dispose();
					_socket = null;
				}
			}
		}

		public async Task WriteMessageAsync(string message)
		{
			if(!DeviceConnecting)
				return;
			_writer.WriteString(message);
			await _writer.StoreAsync();
			await _writer.FlushAsync();
		}
		//TODO: Тут либо устанавливаем точное количество байт на сообщение, либо сначала шлем сколько байт занимает сообщение и потом читаем само сообщение
		private async void ReceiveStringLoop()
		{
			var size = await _reader.LoadAsync(32);
			var receivedStrings = string.Empty; 
			while (_reader.UnconsumedBufferLength > 0)
			{
			// 	uint bytesToRead = _reader.ReadUInt32();
			receivedStrings += _reader.ReadString(size);
			}
			if (!string.IsNullOrEmpty(receivedStrings))
			{
				ReadMessage(receivedStrings);
			}
			
			// if (size < sizeof(uint))
			// {
			// 	DisconnectDevice();
			// 	return;
			// }
			// 	
			// var stringLength = _reader.ReadInt32();
			// var actualStringLength = await _reader.LoadAsync((uint)stringLength);
			// if (actualStringLength != stringLength)
			// {
			// 	DisconnectDevice();
			// 	return;
			// }
			//
			// receivedStrings = _reader.ReadString((uint) stringLength);
			// ReadMessage(receivedStrings);
			ReceiveStringLoop();
		}
	}
}