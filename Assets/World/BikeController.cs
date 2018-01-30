using UnityEngine;
using System.Collections;
using System;
using System.IO.Ports;
using System.Threading;

public class BikeController : MonoBehaviour {
	static SerialPort port = null;
	static string portName = "COM3";
	static byte address = 0;
	static int rpm = 0;
	
	static void RequestAddress() {
		byte[] buffer = { 0x11 };
		port.Write(buffer, 0, buffer.Length);
		Console.WriteLine("Sent data.");
		Thread.Sleep(100);
	}


	static void RequestRPM() {
		byte[] buffer = { 0x40, address };
		port.Write(buffer, 0, buffer.Length);
		Console.WriteLine("Sent data.");
		//Thread.Sleep(100);
	}

	static byte ReadAddress() {
		byte address = 0;
		Console.WriteLine("Reading data.");
		for (int i = 0; i < 100; ++i) {
			try {
				int b = port.ReadByte();
				if (i == 1) address = (byte)b;
			}
			catch (TimeoutException) {
				break;
			}
		}
		return address;
	}

	static int ReadRPM() {
		int rpm = 0;
		Console.WriteLine("Reading data.");
		for (int i = 0; i < 100; ++i) {
			try {
				int b = port.ReadByte();
				if (i == 6) rpm = b;
			}
			catch (TimeoutException) {
				break;
			}
		}
		return rpm;
	}

	static void Run() {
		port = new SerialPort("COM3");
		port.BaudRate = 9600;
		port.Parity = Parity.None;
		port.StopBits = StopBits.One;
		port.DataBits = 8;
		port.ReadTimeout = 50; // 1 ms bis timeout

		port.Handshake = Handshake.None;
		port.Open(); // could take some time

		RequestAddress();
		address = ReadAddress();
		for (;;) {
			RequestRPM();
			rpm = ReadRPM();
			//Console.WriteLine("RPM: " + rpm);
		}
	}

	public static int getRPM() {
		return rpm;
	}

	public static void Initialize(string comPort = "COM3") {
		portName = comPort;
		Thread thread = new Thread(Run);
		thread.Start();
	}
}
