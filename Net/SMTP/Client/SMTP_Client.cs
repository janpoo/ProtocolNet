using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Net.Sockets;
using System.Windows.Forms;
using LumiSoft.Net.Dns;

namespace LumiSoft.Net.SMTP.Client
{
	#region enum SMTP_ErrorType

	/// <summary>
	/// SMTP error types.
	/// </summary>
	public enum SMTP_ErrorType
	{
		/// <summary>
		/// Connection related error.
		/// </summary>
		ConnectionError = 1,

		/// <summary>
		/// Email address doesn't exist.
		/// </summary>
		InvalidEmailAddress = 2,

	//	MessageSizeExceeded = 3,

		/// <summary>
		/// Some feature isn't supported.
		/// </summary>
		NotSupported = 4,

		/// <summary>
		/// Unknown error.
		/// </summary>
		UnKnown = 256,
	}

	#endregion

	#region Event handlers

	/// <summary>
	/// 
	/// </summary>
	public delegate void SMTP_Error_EventHandler(object sender,SMTP_Error e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void SMTP_PartOfMessage_EventHandler(object sender,PartOfMessage_EventArgs e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void SMTP_SendJob_EventHandler(object sender,SendJob_EventArgs e);

	#endregion

	/// <summary>
	/// SMTP Client.
	/// </summary>
	/// <example>
	/// <code>
	/// SMTP_Client c = new SMTP_Client();
	/// c.UseSmartHost = false;
	/// c.DnsServers = new string[]{"194.126.115.18"};
	///	
	///	// Construct,load message here.
	///	// You can use here MimeConstructor to create new message.
	///	MemoryStream msgStrm = new MemoryStream();
	///	
	///	c.Send(new string[]{"recipeint@xx.ww"},"sender@dd.rr",msgStrm);
	///					
	/// </code>
	/// </example>
	public class SMTP_Client
	{
		private string     m_HostName     = "";
		private string     m_SmartHost    = "";
		private string[]   m_DnsServers   = null;
		private bool       m_UseSmartHost = false;
		private int        m_Port         = 25;
		private int        m_MaxThreads   = 10;
//		private int        m_SendTimeOut  = 30000;
		private ArrayList  m_pErrors      = null;
		private Hashtable  m_SendTrTable  = null;
		private Control    m_pOwnerUI     = null;
		private object[]   m_Params       = null;
		private _LogWriter m_pLogWriter   = null;


		#region Events declarations

		/// <summary>
		/// Is raised when some send jobs message part is sent.
		/// </summary>
		public event SMTP_PartOfMessage_EventHandler PartOfMessageIsSent = null;

		/// <summary>
		/// Is raised when new send job starts.
		/// </summary>
		public event SMTP_SendJob_EventHandler NewSendJob = null;

		/// <summary>
		/// Is raised when send job completes.
		/// </summary>
		public event SMTP_SendJob_EventHandler SendJobCompleted = null;

		/// <summary>
		/// Is raised when error occurs.
		/// </summary>
		public event SMTP_Error_EventHandler Error = null;

		/// <summary>
		/// Is raised when all sedjobs are completed.
		/// </summary>
		public event System.EventHandler CompletedAll = null;

		/// <summary>
		/// Occurs when SMTP session has finished and session log is available.
		/// </summary>
		public event LogEventHandler SessionLog = null;

		#endregion


		/// <summary>
		/// Default constructor.
		/// </summary>
		public SMTP_Client() : this(null)
		{
		}

		/// <summary>
		/// Use this constructor if you use this component on UI component.
		/// NOTE: Events are invoked on UI Thread.
		/// </summary>
		/// <param name="ownerUI"></param>
		public SMTP_Client(Control ownerUI)
		{	
			m_pOwnerUI    = ownerUI;
			m_pErrors     = new ArrayList();
			m_SendTrTable = new Hashtable();
		//	m_pLogWriter  = new _LogWriter(this.SessionLog);

			try{
				m_HostName = System.Net.Dns.GetHostName();
			}
			catch{
				m_HostName = "UnKnown";
			}
		}


		#region function BeginSend

		/// <summary>
		/// Starts asynchronous sending.
		/// </summary>
		/// <param name="to">Recipients, may be from different e-domain when using dns or relay is allowed in smart host.</param>
		/// <param name="from">Sendres email address.</param>
		/// <param name="message">Stream which contains message. NOTE: reading from stream is started from stream current position.</param>
		public void BeginSend(string[] to,string from,Stream message)
		{	
			// Clear old errors
			m_pErrors.Clear();

			m_Params = new object[]{to,from,message};

			// Start new thread for send controller
			Thread t = new Thread(new ThreadStart(this.BeginSend));	
			t.Start();
		}

		/// <summary>
		/// This function just controls sending.
		/// </summary>
		private void BeginSend()
		{
			string[] to    = (string[])m_Params[0];
			string from    = (string)m_Params[1];
			Stream message = (Stream)m_Params[2];
			

			// Check to if same e-domians(same server). Eg. ivx@lumisoft.ee, xxx@lumisoft.ee
			// If not, group same e-domians and send them appropiate server. Eg. ivx@lumisoft.ee, aa@ve.ee
			// This approach avoids sending message multiple times to same server.
			// Eg. we have ivx@lumisoft.ee, xxx@lumisoft.ee,  aa@ve.ee
			// Then we must send ivx@lumisoft.ee, xxx@lumisoft.ee - to lumisoft server
			//					 aa@ve.ee - to ve server

			Hashtable rcptPerServer = new Hashtable();
			foreach(string rcpt in to){
				string eDomain = rcpt.Substring(rcpt.IndexOf("@"));//*******
				if(rcptPerServer.Contains(eDomain)){
					// User eAddress is in same server
					ArrayList sameServerEaddresses = (ArrayList)rcptPerServer[eDomain];
					sameServerEaddresses.Add(rcpt);
				}
				else{
					ArrayList sameServerEaddresses = new ArrayList();
					sameServerEaddresses.Add(rcpt);
					rcptPerServer.Add(eDomain,sameServerEaddresses);
				}
			}

			// Loop through the list of servers where we must send messages
			foreach(ArrayList sameServerEaddresses in rcptPerServer.Values){
				string[] rcpts = new string[sameServerEaddresses.Count];
				sameServerEaddresses.CopyTo(rcpts);

				//----- Create copy of message ------------------------------------//
				// We neeed this, because otherwise multible Threads access Stream.
				long pos = message.Position;
				byte[] dataBuf = new byte[message.Length - message.Position];
				message.Read(dataBuf,0,(int)(message.Length - message.Position));
				MemoryStream messageCopy = new MemoryStream(dataBuf);
				message.Position = pos;

				// Start new thread for sending
				Thread t = new Thread(new ThreadStart(this.StartSendJob));					
				m_SendTrTable.Add(t,new object[]{rcpts,from,messageCopy});
				t.Start();
					
				//If maximum sender threads are exceeded,
				//wait when some gets available.
				while(m_SendTrTable.Count > m_MaxThreads){
					Thread.Sleep(100);
				}
			}

			// Wait while all send jobs complete
			while(m_SendTrTable.Count > 0){
				Thread.Sleep(300);
			}

			OnCompletedAll();
		}

		#endregion

		#region function Send

		/// <summary>
		/// Sends message.
		/// </summary>
		/// <param name="to">Recipients. NOTE: recipients must be desination server recipients or when using SmartHost, then smart host must allow relay.</param>
		/// <param name="from">Sendrer's email address.</param>
		/// <param name="message">tream which contains message. NOTE: reading from stream is started from stream current position.</param>
		/// <returns>Returns true if send comeleted successfully.</returns>
		public bool Send(string[] to,string from,Stream message)
		{
			// Clear old errors
			m_pErrors.Clear();

			// ToDo split to TO ******

			// Check to if same e-domians(same server). Eg. ivx@lumisoft.ee, xxx@lumisoft.ee
			// If not, group same e-domians and send them appropiate server. Eg. ivx@lumisoft.ee, aa@ve.ee
			// This approach avoids sending message multiple times to same server.
			// Eg. we have ivx@lumisoft.ee, xxx@lumisoft.ee,  aa@ve.ee
			// Then we must send ivx@lumisoft.ee, xxx@lumisoft.ee - to lumisoft server
			//					 aa@ve.ee - to ve server

			bool sendOk = SendMessageToServer(to,from,message);

			if(m_pLogWriter != null){
				m_pLogWriter.Flush();
			}

			return sendOk;
		}

		#endregion


		#region function StartSendJob

		private void StartSendJob()
		{
			try
			{
				if(!m_SendTrTable.Contains(Thread.CurrentThread)){
					return;
				}
								
				// Get params from Hashtable
				object[] param = (object[])m_SendTrTable[Thread.CurrentThread];

				// Raise event
				OnNewSendJobStarted(Thread.CurrentThread.GetHashCode().ToString(),(string[])param[0]);

				// Send message to specified server
				SendMessageToServer((string[])param[0],(string)param[1],(Stream)param[2]);				
				
			}
			catch{
			}
			finally{
				RemoveSenderThread(Thread.CurrentThread);
			}
		}

		#endregion

		#region function RemoveThread

		/// <summary>
		/// Removes sender Thread - Thread has finnished sending.
		/// </summary>
		/// <param name="t"></param>
		private void RemoveSenderThread(Thread t)
		{
			lock(m_SendTrTable){				
				if(!m_SendTrTable.ContainsKey(t)){
					return;
				}
				m_SendTrTable.Remove(t);				
			}
		}

		#endregion


		//---- SMTP implementation ----//

		#region function SendMessageToServer

		private bool SendMessageToServer(string[] to,string reverse_path,Stream message)
		{
			// Get email from to string
			for(int i=0;i<to.Length;i++){
				to[i] = (new LumiSoft.Net.Mime.Parser.eAddress(to[i])).Email;
			}

			ArrayList defectiveEmails = new ArrayList();

			Socket socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);

			try{
				string reply = "";	
				bool supports_SIZE  = false;
				bool supports_8BIT  = false;
				bool supports_BDAT  = false;
										
				if(m_UseSmartHost){
					IPEndPoint ipdest = new IPEndPoint(System.Net.Dns.Resolve(m_SmartHost).AddressList[0],m_Port);
					socket.Connect(ipdest);					
				}
				else{
					//---- Parse e-domain -------------------------------//
					string domain = to[0];

					// eg. Ivx <ivx@lumisoft.ee>
					if(domain.IndexOf("<") > -1 && domain.IndexOf(">") > -1){
						domain = domain.Substring(domain.IndexOf("<")+1,domain.IndexOf(">") - domain.IndexOf("<")-1);
					}

					if(domain.IndexOf("@") > -1){
						domain = domain.Substring(domain.LastIndexOf("@") + 1);
					}

					//--- Get MX record -------------------------------------------//
					DnsEx dns = new DnsEx();
					DnsEx.DnsServers = m_DnsServers;
					MX_Record[] mxRecords = null;
					DnsReplyCode replyCode = dns.GetMXRecords(domain,out mxRecords);
					switch(replyCode)
					{
						case DnsReplyCode.Ok:
							// Try all available hosts by MX preference order, if can't connect specified host.
							foreach(MX_Record mx in mxRecords){
								try{
									socket.Connect(new IPEndPoint(System.Net.Dns.Resolve(mx.Host).AddressList[0],m_Port));
									break;
								}
								catch{ // Just skip and let for to try next host.
								}
							}							
							break;

						case DnsReplyCode.NoEntries:
							/* Rfc 2821 5
							If   no MX records are found, but an A RR is found, the A RR is treated as
							if it was associated with an implicit MX RR, with a preference of 0,
							pointing to that host.

							*/
							try // Try to connect with A record
							{
								IPHostEntry ipEntry = System.Net.Dns.Resolve(domain);
								socket.Connect(new IPEndPoint(ipEntry.AddressList[0],m_Port));
							}
							catch{
								OnError(SMTP_ErrorType.InvalidEmailAddress,to,"email domain <" + domain + "> is invalid");
							
								defectiveEmails.AddRange(to);
								return false;
							}
							break;

						case DnsReplyCode.TempError:
							string dnsServers = "";
							foreach(string s in m_DnsServers){
								dnsServers += s + ";";
							}
							throw new Exception("Error retrieving MX record for domain '" + domain + "' with dns servers:{" + dnsServers + "}.");
					}					
				}
											

				#region Get 220 reply from server 
				/* NOTE: reply may be multiline
				   220 xx ready
				    or
				   220-someBull
				   200 xx
				*/ 

				// Server must reply 220 - Server OK
				reply = ReadLine(socket);				
				if(!IsReplyCode("220",reply)){
					OnError(SMTP_ErrorType.UnKnown,to,reply);
					SendLine(socket,"QUIT");
					return false;
				}
				else{
					// 220-xxx<CRLF>
					// 220 aa<CRLF> - means end
					// reply isn't complete, get more
					while(reply.IndexOf("220 ") == -1){
						reply += ReadLine(socket);
					}
				}

				#endregion


				#region cmd EHLO/HELO
	
				// Send greeting to server
				SendLine(socket,"EHLO " + m_HostName);

				reply = ReadLine(socket);
				if(!IsReplyCode("250",reply)){
					// EHLO failed, mayby server doesn't support it, try HELO
					SendLine(socket,"HELO " + m_HostName);
					reply = ReadLine(socket);
					if(!IsReplyCode("250",reply)){
						OnError(SMTP_ErrorType.UnKnown,to,reply);
						SendLine(socket,"QUIT");

						defectiveEmails.AddRange(to);
						return false;
					}
				//	else{
				//		supports_ESMTP = false;
				//	}
				}
				else{
					// 250-xxx<CRLF>
					// 250 aa<CRLF> - means end
					// reply isn't complete, get more
					while(reply.IndexOf("250 ") == -1){
						reply += ReadLine(socket);
					}

					// Check if SIZE argument is supported
					if(reply.ToUpper().IndexOf("SIZE") > -1){
						supports_SIZE = true;
					}

					// Check if 8BITMIME argument is supported
					if(reply.ToUpper().IndexOf("8BITMIME") > -1){
						supports_8BIT = true;
					}
					
					// Check if CHUNKING argument is supported
					if(reply.ToUpper().IndexOf("CHUNKING") > -1){
						supports_BDAT = true;
					}
				}
				
				#endregion
	
				// If server doesn't support 8bit mime, check if message is 8bit.
				// If is we MAY NOT send this message or loss of data
				if(!supports_8BIT){
					if(Is8BitMime(message)){
						OnError(SMTP_ErrorType.NotSupported,to,"Message is 8-Bit mime and server doesn't support it.");
						SendLine(socket,"QUIT");
						return false;	
					}
				}

				#region cmd MAIL
				// NOTE: Syntax:{MAIL FROM:<ivar@lumisoft.ee> [SIZE=msgSize]<CRLF>}
								
				// Send Mail From
				if(supports_SIZE){
					SendLine(socket,"MAIL FROM:<" + reverse_path + "> SIZE=" + message.Length.ToString());
				}
				else{
					SendLine(socket,"MAIL FROM:<" + reverse_path + ">");
				}

				reply = ReadLine(socket);
				if(!IsReplyCode("250",reply)){
					// To Do: Check if size exceeded error:

					OnError(SMTP_ErrorType.UnKnown,to,reply);
					SendLine(socket,"QUIT");

					defectiveEmails.AddRange(to);
					return false;
				}

				#endregion

				#region cmd RCPT
				// NOTE: Syntax:{RCPT TO:<ivar@lumisoft.ee><CRLF>}
				
				bool isAnyValidEmail = false;
				foreach(string rcpt in to){
					// Send Mail To
					SendLine(socket,"RCPT TO:<" + rcpt + ">");

					reply = ReadLine(socket);
					if(!IsReplyCode("250",reply)){
						// Is unknown user
						if(IsReplyCode("550",reply)){							
							OnError(SMTP_ErrorType.InvalidEmailAddress,new string[]{rcpt},reply);
						}
						else{
							OnError(SMTP_ErrorType.UnKnown,new string[]{rcpt},reply);
						}

						defectiveEmails.Add(rcpt);
					}
					else{
						isAnyValidEmail = true;
					}
				}

				// If there isn't any valid email - quit.
				if(!isAnyValidEmail){
					SendLine(socket,"QUIT");
					return false;
				}
				//---------------------------------------------//

				#endregion


				#region cmd DATA

				if(!supports_BDAT){

					// Notify Data Start
					SendLine(socket,"DATA");

					reply = ReadLine(socket);
					if(!IsReplyCode("354",reply)){
						OnError(SMTP_ErrorType.UnKnown,to,reply);
						SendLine(socket,"QUIT");

						defectiveEmails.AddRange(to);
						return false;
					}
								
					//------- Do period handling -----------------------------------------//
					// If line starts with '.', add additional '.'.(Read rfc for more info)
					MemoryStream msgStrmPeriodOk = Core.DoPeriodHandling(message,true,false);
					//--------------------------------------------------------------------//
					
					// Check if message ends with <CRLF>, if not add it. -------//
					if(msgStrmPeriodOk.Length >= 2){
						byte[] byteEnd = new byte[2];
						msgStrmPeriodOk.Position = msgStrmPeriodOk.Length - 2;
						msgStrmPeriodOk.Read(byteEnd,0,2);

						if(byteEnd[0] != (byte)'\r' && byteEnd[1] != (byte)'\n'){
							msgStrmPeriodOk.Write(new byte[]{(byte)'\r',(byte)'\n'},0,2);
						}
					}
					msgStrmPeriodOk.Position = 0;
					//-----------------------------------------------------------//

					//---- Send message --------------------------------------------//
					long totalSent   = 0;
					long totalLength = msgStrmPeriodOk.Length; 
					while(totalSent < totalLength){
						byte[] dataBuf = new byte[4000];
						int nCount = msgStrmPeriodOk.Read(dataBuf,0,dataBuf.Length);
						int countSended = socket.Send(dataBuf,0,nCount,SocketFlags.None);					
						totalSent += countSended;

						if(countSended != nCount){
							msgStrmPeriodOk.Position = totalSent;
						}

						OnPartOfMessageIsSent(countSended,totalSent,totalLength);
					}
					//-------------------------------------------------------------//
					msgStrmPeriodOk.Close();
			
					// Notify End of Data
					SendLine(socket,".");

					reply = ReadLine(socket);
					if(!IsReplyCode("250",reply)){
						OnError(SMTP_ErrorType.UnKnown,to,reply);
						SendLine(socket,"QUIT");

						defectiveEmails.AddRange(to);
						return false;
					}
				}

				#endregion

				#region cmd BDAT

				if(supports_BDAT){
					SendLine(socket,"BDAT " + (message.Length - message.Position) + " LAST");

					//---- Send message --------------------------------------------//
					long totalSent   = 0;
					long totalLength = message.Length - message.Position; 
					while(totalSent < totalLength){
						byte[] dataBuf = new byte[4000];
						int nCount = message.Read(dataBuf,0,dataBuf.Length);
						int countSended = socket.Send(dataBuf,0,nCount,SocketFlags.None);					
						totalSent += countSended;

						if(countSended != nCount){
							message.Position = totalSent;
						}

						OnPartOfMessageIsSent(countSended,totalSent,totalLength);
					}
					//-------------------------------------------------------------//

					// Get store result
					reply = ReadLine(socket);
					if(!reply.StartsWith("250")){
						OnError(SMTP_ErrorType.UnKnown,to,reply);
						SendLine(socket,"QUIT");

						defectiveEmails.AddRange(to);
						return false;
					}
				}

				#endregion

				#region cmd QUIT

				// Notify server - server can exit now
				SendLine(socket,"QUIT");

				reply = ReadLine(socket);

				#endregion
				
			}
			catch(Exception x){
				OnError(SMTP_ErrorType.UnKnown,to,x.Message);

				defectiveEmails.AddRange(to);
				return false;
			}
			finally{
				// Raise event
				OnSendJobCompleted(Thread.CurrentThread.GetHashCode().ToString(),to,defectiveEmails);

				socket.Close();
			}

			return true;
		}

		#endregion

		#region function IsReplyCode

		/// <summary>
		/// Checks if reply code.
		/// </summary>
		/// <param name="replyCode">Replay code to check.</param>
		/// <param name="reply">Full repaly.</param>
		/// <returns>Retruns true if reply is as specified.</returns>
		private bool IsReplyCode(string replyCode,string reply)
		{
			if(reply.IndexOf(replyCode) > -1){
				return true;
			}
			else{
				return false;
			}
		}

		#endregion
		
		//----------------------------//


		#region method ReadLine

		private string ReadLine(Socket socket)
		{
			string line = Core.ReadLine(socket);

			if(m_pLogWriter != null){
				m_pLogWriter.AddEntry(line,socket.GetHashCode().ToString(),((IPEndPoint)socket.RemoteEndPoint).Address.ToString(),"C");
			}

			return line;
		}

		#endregion

		#region method SendLine

		private void SendLine(Socket socket,string line)
		{
			Core.SendLine(socket,line);

			if(m_pLogWriter != null){
				m_pLogWriter.AddEntry(line,socket.GetHashCode().ToString(),((IPEndPoint)socket.RemoteEndPoint).Address.ToString(),"S");
			}
		}

		#endregion


		#region function Is8BitMime

		private bool Is8BitMime(Stream strm)
		{
			bool retVal = false;
			long pos = strm.Position;

			TextReader reader = new StreamReader(strm);

			string line = reader.ReadLine();
			while(line != null){
				if(line.ToUpper().IndexOf("CONTENT-TR") > -1){					
					if(line.ToUpper().IndexOf("8BIT") > -1){
						retVal = true;
						break; // Contains 8bit mime
					}
				}
				line = reader.ReadLine();
			}

			// Restore stream position
			strm.Position = pos;

			return retVal;
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Gets or sets host name reported to SMTP server. Eg. 'mail.yourserver.net'.
		/// </summary>
		public string HostName
		{
			get{ return m_HostName; }

			set{
				if(value.Length > 0){
					m_HostName = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets smart host. Eg. 'mail.yourserver.net'.
		/// </summary>
		public string SmartHost
		{
			get{ return m_SmartHost; }

			set{ m_SmartHost = value; }
		}

		/// <summary>
		/// Gets or sets dns servers(IP addresses).
		/// </summary>
		public string[] DnsServers
		{
			get{ return m_DnsServers; }
			
			set{ m_DnsServers = value; }
		}

		/// <summary>
		/// Gets or sets if mail is sent through smart host or using dns.
		/// </summary>
		public bool UseSmartHost
		{
			get{ return m_UseSmartHost; }

			set{ m_UseSmartHost = value; }
		}

		/// <summary>
		/// Gets or sets SMTP port.
		/// </summary>
		public int Port
		{
			get{ return m_Port; }

			set{ m_Port = value; }
		}

		/// <summary>
		/// Gets or sets maximum sender Threads.
		/// </summary>
		public int MaxSenderThreads
		{
			get{ return m_MaxThreads; }

			set{
				if(value < 1){
					m_MaxThreads = 1; 
				}
				else{
					m_MaxThreads = value; 
				}
			}
		}

		/// <summary>
		/// Gets last send attempt errors.
		/// </summary>
		public SMTP_Error[] Errors
		{
			get{ 
				SMTP_Error[] errors = new SMTP_Error[m_pErrors.Count];
				m_pErrors.CopyTo(errors);

				return errors;
			}
		}

		/// <summary>
		/// Gets or sets if to log commands. NOTE: attach events before setting this property true.
		/// </summary>
		public bool LogCommands
		{
			get{ 
				if(m_pLogWriter != null){
					return true;
				}
				else{
					return false;
				}				
			}

			set{
				if(value){
					m_pLogWriter = new _LogWriter(this.SessionLog);
				}
				else{
					m_pLogWriter  = null;
				}
			}
		}

		/// <summary>
		/// Gets if some send job is active.
		/// </summary>
		public bool IsSending
		{
			get{
				if(m_SendTrTable.Count > 0){
					return true;
				}				
				return false;
			}
		}

		#endregion

		#region Events Implementation

		#region function OnPartOfMessageIsSent

		/// <summary>
		/// Raises PartOfMessageIsSent event.
		/// </summary>
		/// <param name="sentBlockSize"></param>
		/// <param name="totalSent"></param>
		/// <param name="messageSize"></param>
		protected void OnPartOfMessageIsSent(long sentBlockSize,long totalSent,long messageSize)
		{			
			if(this.PartOfMessageIsSent != null){
				string jobID = Thread.CurrentThread.GetHashCode().ToString();
				if(m_pOwnerUI == null){
					this.PartOfMessageIsSent(this,new PartOfMessage_EventArgs(jobID,sentBlockSize,totalSent,messageSize));
				}
				// For UI we must use invoke, because UI doesn't support multi Threading.
				else{
					m_pOwnerUI.Invoke(this.PartOfMessageIsSent,new object[]{this,new PartOfMessage_EventArgs(jobID,sentBlockSize,totalSent,messageSize)});
				}
			}
		}

		#endregion

		#region function OnNewSendJobStarted

		/// <summary>
		/// Raises NewSendJob event.
		/// </summary>
		/// <param name="jobID"></param>
		/// <param name="to"></param>
		protected void OnNewSendJobStarted(string jobID,string[] to)
		{
			if(this.NewSendJob != null){
				if(m_pOwnerUI == null){
					this.NewSendJob(this,new SendJob_EventArgs(jobID,to));
				}
				// For UI we must use invoke, because UI doesn't support multi Threading.
				else{
					m_pOwnerUI.Invoke(this.NewSendJob,new object[]{this,new SendJob_EventArgs(jobID,to)});
				}
			}
		}

		#endregion

		#region function OnSendJobCompleted

		/// <summary>
		/// Raises SendJobCompleted event.
		/// </summary>
		/// <param name="jobID"></param>
		/// <param name="to"></param>
		/// <param name="defectiveEmails"></param>
		protected void OnSendJobCompleted(string jobID,string[] to,ArrayList defectiveEmails)
		{
			if(this.SendJobCompleted != null){
				string[] defEmails = new string[defectiveEmails.Count];
				defectiveEmails.CopyTo(defEmails);

				if(m_pOwnerUI == null){
					this.SendJobCompleted(this,new SendJob_EventArgs(jobID,to,defEmails));
				}
				// For UI we must use invoke, because UI doesn't support multi Threading.
				else{
					m_pOwnerUI.Invoke(this.SendJobCompleted,new object[]{this,new SendJob_EventArgs(jobID,to,defEmails)});
				}
			}
		}

		#endregion

		#region function OnCompletedAll

		/// <summary>
		/// Raises CompletedAll event.
		/// </summary>
		private void OnCompletedAll()
		{
			if(m_pLogWriter != null){
				m_pLogWriter.Flush();
			}

			if(this.CompletedAll != null){
				this.CompletedAll(this,new EventArgs());
			}
		}

		#endregion

		#region function OnError

		/// <summary>
		/// Raises Error event.
		/// </summary>
		/// <param name="type">Error type.</param>
		/// <param name="affectedAddresses">Affected email addresses.</param>
		/// <param name="errorText">Error text.</param>
		protected void OnError(SMTP_ErrorType type,string[] affectedAddresses,string errorText)
		{
			// we must lock write(add), becuse multiple Threads may raise OnError same time.
			lock(m_pErrors){
				m_pErrors.Add(new SMTP_Error(type,affectedAddresses,errorText));
			}

			if(this.Error != null){
				if(m_pOwnerUI == null){
					this.Error(this,new SMTP_Error(type,affectedAddresses,errorText));
				}
				// For UI we must use invoke, because UI doesn't support multi Threading.
				else{
					m_pOwnerUI.Invoke(this.Error,new object[]{this,new SMTP_Error(type,affectedAddresses,errorText)});
				}
			}
		}

		#endregion

		#endregion

	}
}
