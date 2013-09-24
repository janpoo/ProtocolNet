using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Security.Cryptography;

namespace LumiSoft.Net.POP3.Client
{
	/// <summary>
	/// POP3 Client.
	/// </summary>
	/// <example>
	/// <code>
	/// using(POP3_Client c = new POP3_Client()){
	///		c.Connect("ivx",110);
	///		c.Authenticate("test","test",true);
	///		
	///		POP3_MessagesInfo mInf = c.GetMessagesInfo();
	///		
	///		// Do your suff
	///	}
	/// </code>
	/// </example>
	public class POP3_Client : IDisposable
	{
		private Socket  m_pSocket        = null;
		private bool    m_Connected     = false;
		private bool    m_Authenticated = false;
		private string  m_ApopHashKey   = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		public POP3_Client()
		{				
		}

		#region function Dispose

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		public void Dispose()
		{
			Disconnect();
		}

		#endregion


		#region function Connect

		/// <summary>
		/// Connects to specified host.
		/// </summary>
		/// <param name="host">Host name.</param>
		/// <param name="port">Port number.</param>
		public void Connect(string host,int port)
		{
			if(!m_Connected){
				m_pSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
				IPEndPoint ipdest = new IPEndPoint(System.Net.Dns.Resolve(host).AddressList[0],port);
				m_pSocket.Connect(ipdest);

			//	Socket s = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
			//	IPEndPoint ipdest = new IPEndPoint(System.Net.Dns.Resolve(host).AddressList[0],port);
			//	s.Connect(ipdest);
			//	s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,1);

			//	m_pSocket = new BufferedSocket(s);

				// Set connected flag
				m_Connected = true;

				string reply = Core.ReadLine(m_pSocket);
				if(reply.StartsWith("+OK")){
					// Try to read APOP hash key, if supports APOP
					if(reply.IndexOf("<") > -1 && reply.IndexOf(">") > -1){
						m_ApopHashKey = reply.Substring(reply.LastIndexOf("<"),reply.LastIndexOf(">") - reply.LastIndexOf("<") + 1);
					}
				}				
			}
		}

		#endregion

		#region function Disconnect

		/// <summary>
		/// Closes connection to POP3 server.
		/// </summary>
		public void Disconnect()
		{
			if(m_pSocket != null){
				// Send QUIT
				Core.SendLine(m_pSocket,"QUIT");			

		//		m_pSocket.Close();
				m_pSocket = null;
			}

			m_Connected     = false;			
			m_Authenticated = false;
		}

		#endregion

		#region function Authenticate

		/// <summary>
		/// Authenticates user.
		/// </summary>
		/// <param name="userName">User login name.</param>
		/// <param name="password">Password.</param>
		/// <param name="tryApop"> If true and POP3 server supports APOP, then APOP is used, otherwise normal login used.</param>
		public void Authenticate(string userName,string password,bool tryApop)
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(m_Authenticated){
				throw new Exception("You are already authenticated !");
			}

			// Supports APOP, use it
			if(tryApop && m_ApopHashKey.Length > 0){
				//--- Compute md5 hash -----------------------------------------------//
				byte[] data = System.Text.Encoding.ASCII.GetBytes(m_ApopHashKey + password);
			
				MD5 md5 = new MD5CryptoServiceProvider();			
				byte[] hash = md5.ComputeHash(data);

				string hexHash = BitConverter.ToString(hash).ToLower().Replace("-","");
				//---------------------------------------------------------------------//

				Core.SendLine(m_pSocket,"APOP " + userName + " " + hexHash);

				string reply = Core.ReadLine(m_pSocket);
				if(reply.StartsWith("+OK")){
					m_Authenticated = true;
				}
				else{
					throw new Exception("Server returned:" + reply);
				}
			}
			else{ // Use normal LOGIN, don't support APOP 
				Core.SendLine(m_pSocket,"USER " + userName);

				string reply = Core.ReadLine(m_pSocket);
				if(reply.StartsWith("+OK")){
					Core.SendLine(m_pSocket,"PASS " + password);

					reply = Core.ReadLine(m_pSocket);
					if(reply.StartsWith("+OK")){
						m_Authenticated = true;
					}
					else{
						throw new Exception("Server returned:" + reply);
					}
				}
				else{
					throw new Exception("Server returned:" + reply);
				}				
			}
		}

		#endregion


		#region function GetMessagesInfo

		/// <summary>
		/// Gets messages info.
		/// </summary>
		public POP3_MessagesInfo GetMessagesInfo()
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			POP3_MessagesInfo messagesInfo = new POP3_MessagesInfo();

			// Before getting list get UIDL list, then we make full message info (UID,Nr,Size).
			Hashtable uidlList = GetUidlList();

			Core.SendLine(m_pSocket,"LIST");

			/* NOTE: If reply is +OK, this is multiline respone and is terminated with '.'.
			Examples:
				C: LIST
				S: +OK 2 messages (320 octets)
				S: 1 120				
				S: 2 200
				S: .
				...
				C: LIST 3
				S: -ERR no such message, only 2 messages in maildrop
			*/

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(line.StartsWith("+OK")){
				// Read lines while get only '.' on line itshelf.
				while(true){
					line = Core.ReadLine(m_pSocket);

					// End of data
					if(line.Trim() == "."){
						break;
					}
					else{
						string[] param = line.Trim().Split(new char[]{' '});
						int  nr   = Convert.ToInt32(param[0]);
						long size = Convert.ToInt64(param[1]);

						messagesInfo.Add(uidlList[nr].ToString(),nr,size);
					}
				}
			}
			else{
				throw new Exception("Server returned:" + line);
			}

			return messagesInfo;
		}

		#endregion

		#region function GetUidlList

		/// <summary>
		/// Gets uid listing.
		/// </summary>
		/// <returns>Returns Hashtable containing uidl listing. Key column contains message NR and value contains message UID.</returns>
		public Hashtable GetUidlList()
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			Hashtable retVal = new Hashtable();

			Core.SendLine(m_pSocket,"UIDL");

			/* NOTE: If reply is +OK, this is multiline respone and is terminated with '.'.
			Examples:
				C: UIDL
				S: +OK
				S: 1 whqtswO00WBw418f9t5JxYwZ
				S: 2 QhdPYR:00WBw1Ph7x7
				S: .
				...
				C: UIDL 3
				S: -ERR no such message
			*/

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(line.StartsWith("+OK")){
				// Read lines while get only '.' on line itshelf.				
				while(true){
					line = Core.ReadLine(m_pSocket);

					// End of data
					if(line.Trim() == "."){
						break;
					}
					else{
						string[] param = line.Trim().Split(new char[]{' '});
						int    nr  = Convert.ToInt32(param[0]);
						string uid = param[1];

						retVal.Add(nr,uid);
					}
				}
			}
			else{
				throw new Exception("Server returned:" + line);
			}

			return retVal;
		}

		#endregion

		#region function GetMessage

		/// <summary>
		/// Transfers specified message to specified socket. This is good for example transfering message from remote POP3 server to POP3 client.
		/// </summary>
		/// <param name="nr">Message number.</param>
		/// <param name="socket">Socket where to store message.</param>
		public void GetMessage(int nr,Socket socket)
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			Core.SendLine(m_pSocket,"RETR " + nr.ToString());

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(line.StartsWith("+OK")){
				NetworkStream readStream  = new NetworkStream(m_pSocket);
				NetworkStream storeStream = new NetworkStream(socket);

				byte[] crlf = new byte[]{(byte)'\r',(byte)'\n'};
				StreamLineReader reader = new StreamLineReader(readStream);
				byte[] lineData = reader.ReadLine();
				while(lineData != null){
					// End of message reached
					if(lineData.Length == 1 && lineData[0] == '.'){
						return;
					}

					storeStream.Write(lineData,0,lineData.Length);
					storeStream.Write(crlf,0,crlf.Length);
					lineData = reader.ReadLine();
				}
			}
			else{
				throw new Exception("Server returned:" + line);
			}
		}

		/// <summary>
		/// Gets specified message.
		/// </summary>
		/// <param name="nr">Message number.</param>
		public byte[] GetMessage(int nr)
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			Core.SendLine(m_pSocket,"RETR " + nr.ToString());

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(line.StartsWith("+OK")){
				MemoryStream strm = new MemoryStream();
				ReadReplyCode code = Core.ReadData(m_pSocket,out strm,null,100000000,30000,"\r\n.\r\n",".\r\n");
				if(code != ReadReplyCode.Ok){
					throw new Exception("Error:" + code.ToString());
				}
				return Core.DoPeriodHandling(strm,false).ToArray();
			}
			else{
				throw new Exception("Server returned:" + line);
			}
		}

		#endregion

		#region function DeleteMessage

		/// <summary>
		/// Deletes specified message
		/// </summary>
		/// <param name="messageNr">Message number.</param>
		public void DeleteMessage(int messageNr)
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			Core.SendLine(m_pSocket,"DELE " + messageNr.ToString());

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(!line.StartsWith("+OK")){
				throw new Exception("Server returned:" + line);
			}
		}

		#endregion

		#region function GetTopOfMessage

		/// <summary>
		/// Gets top lines of message.
		/// </summary>
		/// <param name="nr">Message number which top lines to get.</param>
		/// <param name="nLines">Number of lines to get.</param>
		public byte[] GetTopOfMessage(int nr,int nLines)
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}
			

			Core.SendLine(m_pSocket,"TOP " + nr.ToString() + " " + nLines.ToString());

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(line.StartsWith("+OK")){
				MemoryStream strm = new MemoryStream();
				ReadReplyCode code = Core.ReadData(m_pSocket,out strm,null,100000000,30000,"\r\n.\r\n",".\r\n");
				if(code != ReadReplyCode.Ok){
					throw new Exception("Error:" + code.ToString());
				}
				return Core.DoPeriodHandling(strm,false).ToArray();
			}
			else{
				throw new Exception("Server returned:" + line);
			}
		}

		#endregion

		#region function Reset

		/// <summary>
		/// Resets session.
		/// </summary>
		public void Reset()
		{
			if(!m_Connected){
				throw new Exception("You must connect first !");
			}

			if(!m_Authenticated){
				throw new Exception("You must authenticate first !");
			}

			Core.SendLine(m_pSocket,"RSET");

			// Read first line of reply, check if it's ok
			string line = Core.ReadLine(m_pSocket);
			if(!line.StartsWith("+OK")){
				throw new Exception("Server returned:" + line);
			}
		}

		#endregion

	}
}
