/*
 *
 * Data Comm Project III
 * Hamming TFTP
 * Jesse martinez (jem4687)
 *
*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;

/// <summary>
/// This class contains all the methods neede to read a file from a TFTP server
/// </summary>
public class HammingTFTP()
{
        //////////////////////////////////////////////////////
	//	Class Variables
	//////////////////////////////////////////////////////

	private const int TFTP_Port = 69;
	private const int fullBlock = 516;

	private string mode;
	private string host;
	private string requestFile;

	private IPAddress hostIP;

	private UdpClient client;

	private IPEndPoint receiveLoc;

	private Dictionary<string, byte> opCodes;

	/// <summary>
	/// Non-Default Constructor
	///
	/// Constructor intitializes all variables with given parameters
	/// </summary>
	///
	/// <param name = "mode"> netascii or octet mode </param>
	/// <param name = "host"> the host name of the TFTP server </param>
	/// <param name = "fileName"> file to retrieve from server </param>
	public HammingTFTP(string mode, string host, string fileName)
		:this()
	{
		this.mode = mode;
		this.host = host;
		this.requestFile = fileName;
		hostIP = null;
		client = new UdpClient();
		receiveLoc = null;
		testHostName();
		fillDictionary();
	}

	/// <summary>
	/// testHostName - attempts to find the IP Address of TFTP server
	/// </summary>
	///
	/// <return> true if IP Adress was found false otherwise </return>
	public void testHostName()
	{
		try
		{
			IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
			hostIP = addresses[0];
		}
		catch(Exception e)
		{
			Console.WriteLine("Host: " + host + " not found");
			hostIP = null;
		}
	}

	/// <summary>
	/// fillDictionary - initializes the opCodes dictionary with the 
	///                  standard TFTP opCodes
	/// </summary>
	public void fillDictionary()
	{
		opCodes = new Dictionary<string, byte>()
		{
			{"read", 1},
			{"write", 2},
			{"data", 3},
			{"ack", 4},
			{"error", 5}
		};
	}

	/// <summary>
	/// retrieveFile - retrieves a file from the TFTP server sending ACK 
	///                after each block is received and resending ACKs
	///		   if necessary, if error occurs stop retreiving blocks
	/// </summary>
	///
	/// <param name = "block"> first block of data retrieved from the 
	///			   server </param>
	public void retrieveFile(byte[] block)
	{
		FileStream fileStream = new FileStream(Directory.GetCurrentDirectory() + "/" + requestFile, FileMode.Create, FileAccess.ReadWrite);
		
		int blockNum = 1;

		//Console.WriteLine(block[3]);
	
		do
		{
			//Console.WriteLine(blockNum);
			//Console.WriteLine(block.Length);
			int blockReceived = (256 * (int)block[2]) + (int)block[3];
			if(blockReceived == blockNum && block[1] == opCodes["data"])
			{
				fileStream.Write(block, 4, block.Length - 4);
				block = sendAck(blockNum, false);
				if((int)block[2] == 255 && (int)block[3] == 255)
					blockNum = 0;
				blockNum += 1;
			}
			else
			{
				block = sendAck(blockNum - 1, false);
			}
		}
		while(block != null && block.Length == fullBlock);
		
		if(block != null)
		{
			sendAck(blockNum, true);
			fileStream.Write(block, 4, block.Length - 4);
			Console.WriteLine("File: " + requestFile + " successfully downloaded");
		}
		
		fileStream.Close();
	}

	/// <summary>
	/// sendAck - send an ACK packet to the server with the given block
	///	      number
	/// </summary>
	///
	/// <param name = "blockNum"> the block number to ACK </param>
	/// <param name = "finished"> true if ACK is for last block false
	///			      otherwise </param>
	///
	/// <return> a byte array of the next received block from the server
	/// </return>
	public byte[] sendAck(int blockNum, bool finished)
	{
		byte[] packet = new byte[4];
		packet[0] = 0;
		packet[1] = opCodes["ack"];
		
		byte[] intBytes = BitConverter.GetBytes(blockNum);
		
		//Console.WriteLine(intBytes[0] + " , " + intBytes[1] + " , " + intBytes[2] + " , " + intBytes[3]);
		
		packet[2] = intBytes[1];
		packet[3] = intBytes[0];

		return sendReceivePacket(packet, receiveLoc, finished);
	}

	/// <summary>
	/// sendRequest - creates a request packet and starts retreiving a file
	///	          if no error occurs
	/// </summary>
	public void sendRequest()
	{
		if(hostIP != null)
		{
			byte[] modeBytes = Encoding.ASCII.GetBytes(mode);
			byte[] fileBytes = Encoding.ASCII.GetBytes(requestFile);
			byte[] requestPacket = new byte[4 + fileBytes.Length  + modeBytes.Length];

			requestPacket[0] = 0;
			requestPacket[1] = opCodes["read"];
			for( int i = 0; i < fileBytes.Length; i++ )
				requestPacket[i + 2] = fileBytes[i];
			requestPacket[fileBytes.Length + 2] = 0;
			for( int i = 0; i < modeBytes.Length; i++ )
				requestPacket[fileBytes.Length + 3 + i] = modeBytes[i];
			requestPacket[requestPacket.Length - 1] = 0;
	
			IPEndPoint destination = new IPEndPoint(hostIP, TFTP_Port);
			byte[] firstBlock = sendReceivePacket(requestPacket, destination, false);
			if(firstBlock != null)
			{
				retrieveFile(firstBlock);
			}
		}	
		closeClient();
	}

	/// <summary>
	/// sendReceivePacket - send a packet to the server and receives a 
	///			packet if finished is false
	/// </summary>
	///
	/// <param name = "packet"> packet to send </param>
	/// <param name = "desination"> IPEndPoint of where to send to </param>
	/// <param name = "finished"> true if done receiving packets false
	///			      otherwise </param>
	///
	/// <return> next packet received or null if error was received or
	///          finished is true
	/// </return>
	public byte[] sendReceivePacket(byte[] packet, IPEndPoint destination, bool finished)
	{
		byte[] receivePacket = null;
		while(receivePacket == null)
		{
			try
			{
				client.Send(packet, packet.Length, destination);
				//int port = destination.Port;
				receiveLoc = destination;
				//new IPEndPoint(hostIP, port);

				if(finished)
					return null;

				receivePacket = client.Receive(ref receiveLoc);
		
				if(!checkError(receivePacket))
					return null;
			}
			catch(SocketException e)
			{
				
			}
		}
		return receivePacket;
	}

	/// <summary>
	/// checkError - checks to see if packet received was an error packet
	/// </summary>
	///
	/// <param name = "packet"> last packet received </param>
	/// 
	/// <return> true if packet wasnt an error packet, false othehrwise
	/// </return>
	public bool checkError(byte[] packet)
	{
		if(packet[1] == opCodes["error"])
		{
			byte[] error = new byte[packet.Length - 5];
			for(int i = 4; i < (packet.Length - 1); i++)
			{
				error[i-4] = packet[i];
			}
			Console.WriteLine("Error code " + packet[0] + packet[1] + ": " + Encoding.ASCII.GetString(error));
			return false;
		}
		return true;
	}

	/// <summary>
	/// closeClient - closes UdpClient used to send and received packets
	/// </summary>
	public void closeClient()
	{
		client.Close();
	}

	/// <summary>
	/// Main - rusn a simple TFTPreader with mode, host and file given as
	///	   command line arguments
	/// </summary>
	static public void Main(string[] args)
	{
		if(args.Length == 3)
		{
			HammingTFTP tftp = new HammingTFTP(args[0], args[1], args[2]);
			tftp.sendRequest();
		}
		else
			Console.WriteLine("Usage: [mono] TFTPreader.exe [netascii|octet] tftp−host file");
	}
}
