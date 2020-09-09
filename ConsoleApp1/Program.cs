using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;

namespace ConsoleApp1
{
    partial class Program
    {
        static async Task Main(string[] args)
        {
            var watcher = new RoboBluetoothWatcher();
            watcher.StartedListening += () =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Начинаем поиск");
            };
            
            watcher.StoppedListening += () =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Заканчиваем поиск");
            };
            watcher.NewDeviceDiscovered += device =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Новое устройство: {device}");
            };
            watcher.DeviceNameChanged += device =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Устройство сменило имя: {device}");
            };
            watcher.DeviceTimeout += device =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Устройство было отключено: {device}");
            };
            watcher.DeviceConnected += device =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"Устройство {device.Name} было подключено");
            };
            watcher.ReadMessage += message =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(message);
            };
            watcher.StartListening();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                var command = Console.ReadLine()?.ToLower().Trim();
                if (string.IsNullOrEmpty(command))
                {
                    var devices = watcher.DiscoverDevices;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"{devices.Count} устройств....");
                    int i = 1;
                    foreach (var device in devices)
                    {
                        Console.WriteLine($"{i}. {device}");
                        i++;
                    }
                    
                } else
                {
                    var deviceNumber = int.Parse(command);
                    await watcher.ConnectDeviceAsync(watcher.DiscoverDevices.ToArray()[deviceNumber - 1].Id);
                    while (true)
                    {
                        Console.Write("Введите сообщение: ");
                        var message = Console.ReadLine().ToLower().Trim();
                        if (message == "exit")
                        {
                            watcher.DisconnectDevice();
                            break;
                        }
                        else
                        {
                            await watcher.WriteMessageAsync(message);
                        }
                        
                    }
                }
            }
        }
    }
}
