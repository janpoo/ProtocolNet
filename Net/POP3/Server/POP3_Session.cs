using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LumiSoft.Net;

namespace LumiSoft.Net.POP3.Server
{
	/// <summary>
	/// POP3 Session.
	/// </summary>
	public class POP3_Session : ISocketServerSession
	{		
		private BufferedSocket m_pSocket        = null;  // Referance to client Socket.
		private POP3_Server    m_pServer        = null;  // Referance to POP3 server.
		private string         m_SessionID      = "";    // Holds session ID.
		private string         m_UserName       = "";    // Holds loggedIn UserName.
		private string         m_Password       = "";    // Holds loggedIn Password.
		private bool           m_Authenticated  = false; // Holds authentication flag.
		private string         m_MD5_prefix     = "";    // Session MD5 prefix for APOP command
		private int            m_BadCmdCount    = 0;     // Holds number of bad commands.
		private POP3_Messages  m_POP3_Messages  = null;		
		private DateTime       m_SessionStartTime;
		private DateTime       m_LastDataTime;
		private _LogWriter     m_pLogWriter     = null;
		private object         m_Tag            = null;
		
		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="clientSocket">Referance to socket.</param>
		/// <param name="server">Referance to POP3 server.</param>
		/// <param name="logWriter">Log writer.</param>
		public POP3_Session(Socket clientSocket,POP3_Server server,_LogWriter logWriter)
		{
			m_pSocket    = new BufferedSocket(clientSocket);
			m_pServer    = server;
			m_pLogWriter = logWriter;

			m_SessionID        = Guid.NewGuid().ToString();
			m_POP3_Messages    = new POP3_Messages();			
			m_SessionStartTime = DateTime.Now;
			m_LastDataTime     = DateTime.Now;

			m_pSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,1);

			// Start session proccessing
			StartSession();
		}

		#region method StartSession

		/// <summary>
		/// Starts session.
		/// </summary>
		private void StartSession()
		{
			// Add session to session list
			m_pServer.AddSession(this);

			if(m_pServer.LogCommands){
				m_pLogWriter.AddEntry("//----- Sys: 'Session:'" + this.SessionID + " added " + DateTime.Now);
			}
	
			try{
				// Check if ip is allowed to connect this computer
				if(m_pServer.OnValidate_IpAddress(this.RemoteEndPoint)){
					// Notify that server is ready
					m_MD5_prefix = "<" + Guid.NewGuid().ToString().ToLower() + ">";
					SendData("+OK " + m_pServer.HostName + " POP3 Server ready " + m_MD5_prefix + "\r\n");

					BeginRecieveCmd();
				}
				else{
					EndSession();
				}
			}
			catch(Exception x){
				OnError(x);
			}
		}

		#endregion

		#region method EndSession

		/// <summary>
		/// Ends session, closes socket.
		/// </summary>
		private void EndSession()
		{
			if(m_pSocket != null){
				m_pSocket.Shutdown(SocketShutdown.Both);
				m_pSocket.Close();
				m_pSocket = null;
			}

			m_pServer.RemoveSession(this);

			// Write logs to log file, if needed
			if(m_pServer.LogCommands){
				m_pLogWriter.AddEntry("//----- Sys: 'Session:'" + this.SessionID + " removed " + DateTime.Now);
				
				m_pLogWriter.Flush();
			}			
		}

		#endregion

	
		#region method OnSessionTimeout

		/// <summary>
		/// Is called by server when session has timed out.
		/// </summary>
		public void OnSessionTimeout()
		{
			try{
				SendData("-ERR Session timeout, closing transmission channel\r\n");
			}
			catch{
			}

			EndSession();
		}

		#endregion

		#region method OnSocketActivity

		/// <summary>
		/// Is called if there was some activity on socket, some data sended or received.
		/// </summary>
		/// <param name="tag"></param>
		private void OnSocketActivity(object tag)
		{
			m_LastDataTime = DateTime.Now;
		}

		#endregion

		#region method OnError

		/// <summary>
		/// Is called when error occures.
		/// </summary>
		/// <param name="x"></param>
		private void OnError(Exception x)
		{
			try{
				if(x is SocketException){
					SocketException xs = (SocketException)x;

					// Client disconnected without shutting down
					if(xs.ErrorCode == 10054 || xs.ErrorCode == 10053){
						if(m_pServer.LogCommands){
							m_pLogWriter.AddEntry("Client aborted/disconnected",this.SessionID,this.RemoteEndPoint.Address.ToString(),"C");
						}

						EndSession();

						// Exception handled, return
						return;
					}
				}

				m_pServer.OnSysError("",x);
			}
			catch(Exception ex){
				m_pServer.OnSysError("",ex);
			}
		}

		#endregion


		#region method BeginRecieveCmd
		
		/// <summary>
		/// Starts recieveing command.
		/// </summary>
		private void BeginRecieveCmd()
		{
			MemoryStream strm = new MemoryStream();
			AsyncSocketHelper.BeginRecieve(m_pSocket,strm,1024,"\r\n","\r\n",strm,new SocketCallBack(this.EndRecieveCmd),new SocketActivityCallback(this.OnSocketActivity));
		}

		#endregion

		#region method EndRecieveCmd

		/// <summary>
		/// Is called if command is recieved.
		/// </summary>
		/// <param name="result"></param>
		/// <param name="exception"></param>
		/// <param name="count"></param>
		/// <param name="tag"></param>
		private void EndRecieveCmd(SocketCallBackResult result,long count,Exception exception,object tag)
		{
			try{
				switch(result)
				{
					case SocketCallBackResult.Ok:
						MemoryStream strm = (MemoryStream)tag;

						string cmdLine = System.Text.Encoding.Default.GetString(strm.ToArray());

						if(m_pServer.LogCommands){
							m_pLogWriter.AddEntry(cmdLine + "<CRLF>",this.SessionID,this.RemoteEndPoint.Address.ToString(),"C");
						}

						// Exceute command
						if(SwitchCommand(cmdLine)){
							// Session end, close session
							EndSession();
						}
						break;

					case SocketCallBackResult.LengthExceeded:
						SendData("-ERR Line too long.\r\n");

						BeginRecieveCmd();
						break;

					case SocketCallBackResult.SocketClosed:
						EndSession();
						break;

					case SocketCallBackResult.Exception:
						OnError(exception);
						break;
				}
			}
			catch(Exception x){
				 OnError(x);
			}
		}

		#endregion
		
		
		#region function SwitchCommand

		/// <summary>
		/// Parses and executes POP3 commmand.
		/// </summary>
		/// <param name="POP3_commandTxt">POP3 command text.</param>
		/// <returns>Returns true,if session must be terminated.</returns>
		private bool SwitchCommand(string POP3_commandTxt)
		{
			//---- Parse command --------------------------------------------------//
			string[] cmdParts = POP3_commandTxt.TrimStart().Split(new char[]{' '});
			string POP3_command = cmdParts[0].ToUpper().Trim();
			string argsText = Core.GetArgsText(POP3_commandTxt,POP3_command);
			//---------------------------------------------------------------------//

			bool getNextCmd = true;

			switch(POP3_command)
			{
				case "USER":
					USER(argsText);
					break;

				case "PASS":
					PASS(argsText);
					break;
					
				case "STAT":
					STAT();
					break;

				case "LIST":
					LIST(argsText);
					break;

				case "RETR":					
					getNextCmd = RETR(argsText);
					break;

				case "DELE":
					DELE(argsText);
					break;

				case "NOOP":
					NOOP();
					break;

				case "RSET":
					RSET();
					break;

				case "QUIT":
					QUIT();
					return true;


				//----- Optional commands ----- //
				case "UIDL":
					UIDL(argsText);
					break;

				case "APOP":
					APOP(argsText);
					break;

				case "TOP":
					getNextCmd = TOP(argsText);					
					break;

				case "AUTH":
					AUTH(argsText);
					break;
										
				default:					
					SendData("-ERR Invalid command\r\n");

					//---- Check that maximum bad commands count isn't exceeded ---------------//
					if(m_BadCmdCount > m_pServer.MaxBadCommands-1){
						SendData("-ERR Too many bad commands, closing transmission channel\r\n");
						return true;
					}
					m_BadCmdCount++;
					//-------------------------------------------------------------------------//
					break;				
			}
			
			if(getNextCmd){
				BeginRecieveCmd();
			}
						
			return false;
		}

		#endregion


		#region function USER

		private void USER(string argsText)
		{
			/* RFC 1939 7. USER
			Arguments:
				a string identifying a mailbox (required), which is of
				significance ONLY to the server
				
			NOTE:
				If the POP3 server responds with a positive
				status indicator ("+OK"), then the client may issue
				either the PASS command to complete the authentication,
				or the QUIT command to terminate the POP3 session.
			 
			*/

			if(m_Authenticated){
				SendData("-ERR You are already authenticated\r\n");
				return;
			}
			if(m_UserName.Length > 0){
				SendData("-ERR username is already specified, please specify password\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There must be only one parameter - userName
			if(argsText.Length > 0 && param.Length == 1){
				string userName = param[0];
							
				// Check if user isn't logged in already
				if(!m_pServer.IsUserLoggedIn(userName)){
					SendData("+OK User:'" + userName + "' ok \r\n");
					m_UserName = userName;
				}
				else{
					SendData("-ERR User:'" + userName + "' already logged in\r\n");
				}
			}
			else{
				SendData("-ERR Syntax error. Syntax:{USER username}\r\n");
			}
		}

		#endregion

		#region function PASS

		private void PASS(string argsText)
		{	
			/* RFC 7. PASS
			Arguments:
				a server/mailbox-specific password (required)
				
			Restrictions:
				may only be given in the AUTHORIZATION state immediately
				after a successful USER command
				
			NOTE:
				When the client issues the PASS command, the POP3 server
				uses the argument pair from the USER and PASS commands to
				determine if the client should be given access to the
				appropriate maildrop.
				
			Possible Responses:
				+OK maildrop locked and ready
				-ERR invalid password
				-ERR unable to lock maildrop
						
			*/

			if(m_Authenticated){
				SendData("-ERR You are already authenticated\r\n");
				return;
			}
			if(m_UserName.Length == 0){
				SendData("-ERR please specify username first\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There may be only one parameter - password
			if(param.Length == 1){
				string password = param[0];
									
				// Authenticate user
				if(m_pServer.OnAuthUser(this,m_UserName,password,"",AuthType.Plain)){					
					m_Password = password;
					m_Authenticated = true;

					// Get user messages info.
					m_pServer.OnGetMessagesInfo(this,m_POP3_Messages);

					SendData("+OK Password ok\r\n");
				}
				else{						
					SendData("-ERR UserName or Password is incorrect\r\n");					
					m_UserName = ""; // Reset userName !!!
				}
			}
			else{
				SendData("-ERR Syntax error. Syntax:{PASS userName}\r\n");
			}
		}

		#endregion

		#region function STAT

		private void STAT()
		{	
			/* RFC 1939 5. STAT
			NOTE:
				The positive response consists of "+OK" followed by a single
				space, the number of messages in the maildrop, a single
				space, and the size of the maildrop in octets.
				
				Note that messages marked as deleted are not counted in
				either total.
			 
			Example:
				C: STAT
				S: +OK 2 320
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}
		
			SendData("+OK " + m_POP3_Messages.Count.ToString() + " " + m_POP3_Messages.GetTotalMessagesSize() + "\r\n");			
		}

		#endregion

		#region function LIST

		private void LIST(string argsText)
		{	
			/* RFC 1939 5. LIST
			Arguments:
				a message-number (optional), which, if present, may NOT
				refer to a message marked as deleted
			 
			NOTE:
				If an argument was given and the POP3 server issues a
				positive response with a line containing information for
				that message.

				If no argument was given and the POP3 server issues a
				positive response, then the response given is multi-line.
				
				Note that messages marked as deleted are not listed.
			
			Examples:
				C: LIST
				S: +OK 2 messages (320 octets)
				S: 1 120				
				S: 2 200
				S: .
				...
				C: LIST 2
				S: +OK 2 200
				...
				C: LIST 3
				S: -ERR no such message, only 2 messages in maildrop
			 
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// Argument isn't specified, multiline response.
			if(argsText.Length == 0){
				string reply = "+OK " + m_POP3_Messages.Count.ToString() + " messages\r\n";

				// Send message number and size for each message
				foreach(POP3_Message msg in m_POP3_Messages.ActiveMessages){
					reply += msg.MessageNr.ToString() + " " + msg.MessageSize + "\r\n";
				}

				// ".<CRLF>" - means end of list
				reply += ".\r\n";

				SendData(reply);
			}
			else{
				// If parameters specified,there may be only one parameter - messageNr
				if(param.Length == 1){
					// Check if messageNr is valid
					if(Core.IsNumber(param[0])){
						int messageNr = Convert.ToInt32(param[0]);
						if(m_POP3_Messages.MessageExists(messageNr)){
							POP3_Message msg = m_POP3_Messages[messageNr];

							SendData("+OK " + messageNr.ToString() + " " + msg.MessageSize + "\r\n");
						}
						else{
							SendData("-ERR no such message, or marked for deletion\r\n");
						}
					}
					else{
						SendData("-ERR message-number is invalid\r\n");
					}
				}
				else{
					SendData("-ERR Syntax error. Syntax:{LIST [messageNr]}\r\n");
				}
			}
		}

		#endregion

		#region function RETR

		/// <summary>
		/// Returns true if command ended syncronously.
		/// </summary>
		private bool RETR(string argsText)
		{
			/* RFC 1939 5. RETR
			Arguments:
				a message-number (required) which may NOT refer to a
				message marked as deleted
			 
			NOTE:
				If the POP3 server issues a positive response, then the
				response given is multi-line.  After the initial +OK, the
				POP3 server sends the message corresponding to the given
				message-number, being careful to byte-stuff the termination
				character (as with all multi-line responses).
				
			Example:
				C: RETR 1
				S: +OK 120 octets
				S: <the POP3 server sends the entire message here>
				S: .
			
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return true;
			}
	
			string[] param = argsText.Split(new char[]{' '});

			// There must be only one parameter - messageNr
			if(argsText.Length > 0 && param.Length == 1){
				// Check if messageNr is valid
				if(Core.IsNumber(param[0])){
					int messageNr = Convert.ToInt32(param[0]);					
					if(m_POP3_Messages.MessageExists(messageNr)){
						POP3_Message msg = m_POP3_Messages[messageNr];
														
						// Raise Event, request message
						byte[] message = m_pServer.OnGetMail(this,msg,m_pSocket.Socket);
						if(message != null){
							//------- Do period handling and send message -----------------------//
							// If line starts with '.', add additional '.'.(Read rfc for more info)
							MemoryStream msgStrm = Core.DoPeriodHandling(message,true);

							byte[] ok     = System.Text.Encoding.Default.GetBytes("+OK\r\n");
							byte[] msgEnd = System.Text.Encoding.Default.GetBytes(".\r\n");

							MemoryStream strm = new MemoryStream();
							strm.Write(ok,0,ok.Length);         // +OK<CRLF>
							msgStrm.WriteTo(strm);              // message data
							strm.Write(msgEnd,0,msgEnd.Length); // .<CRLF>
							strm.Position = 0;

							// Send message asynchronously to client
							BeginSend(strm);
							
							return false;				
						}
						else{									
							SendData("-ERR no such message\r\n");
						}
					}
					else{
						SendData("-ERR no such message\r\n");
					}
				}
				else{
					SendData("-ERR message-number is invalid\r\n");
				}
			}
			else{
				SendData("-ERR Syntax error. Syntax:{RETR messageNr}\r\n");
			}
		
			return true;
		}

		#endregion

		#region function DELE

		private void DELE(string argsText)
		{	
			/* RFC 1939 5. DELE
			Arguments:
				a message-number (required) which may NOT refer to a
				message marked as deleted
			 
			NOTE:
				The POP3 server marks the message as deleted.  Any future
				reference to the message-number associated with the message
				in a POP3 command generates an error.  The POP3 server does
				not actually delete the message until the POP3 session
				enters the UPDATE state.
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There must be only one parameter - messageNr
			if(argsText.Length > 0 && param.Length == 1){
				// Check if messageNr is valid
				if(Core.IsNumber(param[0])){
					int nr = Convert.ToInt32(param[0]);					
					if(m_POP3_Messages.MessageExists(nr)){
						POP3_Message msg = m_POP3_Messages[nr];
						msg.MarkedForDelete = true;

						SendData("+OK marked for delete\r\n");
					}
					else{
						SendData("-ERR no such message\r\n");
					}
				}
				else{
					SendData("-ERR message-number is invalid\r\n");
				}
			}
			else{
				SendData("-ERR Syntax error. Syntax:{DELE messageNr}\r\n");
			}
		}

		#endregion

		#region function NOOP

		private void NOOP()
		{
			/* RFC 1939 5. NOOP
			NOTE:
				The POP3 server does nothing, it merely replies with a
				positive response.
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}

			SendData("+OK\r\n");
		}

		#endregion

		#region function RSET

		private void RSET()
		{
			/* RFC 1939 5. RSET
			Discussion:
				If any messages have been marked as deleted by the POP3
				server, they are unmarked.  The POP3 server then replies
				with a positive response.
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}

			Reset();

			// Raise SessionResetted event
			m_pServer.OnSessionResetted(this);

			SendData("+OK\r\n");
		}

		#endregion

		#region function QUIT

		private void QUIT()
		{
			/* RFC 1939 6. QUIT
			NOTE:
				The POP3 server removes all messages marked as deleted
				from the maildrop and replies as to the status of this
				operation.  If there is an error, such as a resource
				shortage, encountered while removing messages, the
				maildrop may result in having some or none of the messages
				marked as deleted be removed.  In no case may the server
				remove any messages not marked as deleted.

				Whether the removal was successful or not, the server
				then releases any exclusive-access lock on the maildrop
				and closes the TCP connection.
			*/					
			Update();

			SendData("+OK POP3 server signing off\r\n");			
		}

		#endregion


		//--- Optional commands

		#region function TOP

		/// <summary>
		/// Returns true if command ended syncronously.
		/// </summary>
		private bool TOP(string argsText)
		{		
			/* RFC 1939 7. TOP
			Arguments:
				a message-number (required) which may NOT refer to to a
				message marked as deleted, and a non-negative number
				of lines (required)
		
			NOTE:
				If the POP3 server issues a positive response, then the
				response given is multi-line.  After the initial +OK, the
				POP3 server sends the headers of the message, the blank
				line separating the headers from the body, and then the
				number of lines of the indicated message's body, being
				careful to byte-stuff the termination character (as with
				all multi-line responses).
			
			Examples:
				C: TOP 1 10
				S: +OK
				S: <the POP3 server sends the headers of the
					message, a blank line, and the first 10 lines
					of the body of the message>
				S: .
                ...
				C: TOP 100 3
				S: -ERR no such message
			 
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return true;
			}

			string[] param = argsText.Split(new char[]{' '});
			
			// There must be at two parameters - messageNr and nrLines
			if(param.Length == 2){
				// Check if messageNr and nrLines is valid
				if(Core.IsNumber(param[0]) && Core.IsNumber(param[1])){
					int messageNr = Convert.ToInt32(param[0]);
					if(m_POP3_Messages.MessageExists(messageNr)){
						POP3_Message msg = m_POP3_Messages[messageNr];

						byte[] lines = m_pServer.OnGetTopLines(this,msg,Convert.ToInt32(param[1]));
						if(lines != null){
							//------- Do period handling and send message -----------------------//
							// If line starts with '.', add additional '.'.(Read rfc for more info)
							MemoryStream msgStrm = Core.DoPeriodHandling(lines,true);

							byte[] ok     = System.Text.Encoding.Default.GetBytes("+OK\r\n");
							byte[] msgEnd = System.Text.Encoding.Default.GetBytes(".\r\n");

							MemoryStream strm = new MemoryStream();
							strm.Write(ok,0,ok.Length);         // +OK<CRLF>
							msgStrm.WriteTo(strm);              // message data
							strm.Write(msgEnd,0,msgEnd.Length); // .<CRLF>
							strm.Position = 0;

							// Send message asynchronously to client
							BeginSend(strm);

							return false;
						}
						else{
							SendData("-ERR no such message\r\n");
						}
					}
					else{
						SendData("-ERR no such message\r\n");
					}
				}
				else{
					SendData("-ERR message-number or number of lines is invalid\r\n");
				}
			}
			else{
				SendData("-ERR Syntax error. Syntax:{TOP messageNr nrLines}\r\n");
			}

			return true;
		}

		#endregion

		#region function UIDL

		private void UIDL(string argsText)
		{
			/* RFC 1939 UIDL [msg]
			Arguments:
			    a message-number (optional), which, if present, may NOT
				refer to a message marked as deleted
				
			NOTE:
				If an argument was given and the POP3 server issues a positive
				response with a line containing information for that message.

				If no argument was given and the POP3 server issues a positive
				response, then the response given is multi-line.  After the
				initial +OK, for each message in the maildrop, the POP3 server
				responds with a line containing information for that message.	
				
			Examples:
				C: UIDL
				S: +OK
				S: 1 whqtswO00WBw418f9t5JxYwZ
				S: 2 QhdPYR:00WBw1Ph7x7
				S: .
				...
				C: UIDL 2
				S: +OK 2 QhdPYR:00WBw1Ph7x7
				...
				C: UIDL 3
				S: -ERR no such message
			*/

			if(!m_Authenticated){
				SendData("-ERR You must authenticate first\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// Argument isn't specified, multiline response.
			if(argsText.Length == 0){
				string reply = "+OK\r\n";

				// Send message number and size for each message
				foreach(POP3_Message msg in m_POP3_Messages.ActiveMessages){
					reply += msg.MessageNr.ToString() + " " + msg.MessageUID + "\r\n";
				}

				// ".<CRLF>" - means end of list
				reply += ".\r\n";

				SendData(reply);
			}
			else{
				// If parameters specified,there may be only one parameter - messageID
				if(param.Length == 1){
					// Check if messageNr is valid
					if(Core.IsNumber(param[0])){
						int messageNr = Convert.ToInt32(param[0]);
						if(m_POP3_Messages.MessageExists(messageNr)){
							POP3_Message msg = m_POP3_Messages[messageNr];

							SendData("+OK " + messageNr.ToString() + " " + msg.MessageUID + "\r\n");
						}
						else{
							SendData("-ERR no such message\r\n");
						}
					}
					else{
						SendData("-ERR message-number is invalid\r\n");
					}
				}
				else{
					SendData("-ERR Syntax error. Syntax:{UIDL [messageNr]}\r\n");
				}
			}	
		}

		#endregion

		#region function APOP

		private void APOP(string argsText)
		{
			/* RFC 1939 7. APOP
			Arguments:
				a string identifying a mailbox and a MD5 digest string
				(both required)
				
			NOTE:
				A POP3 server which implements the APOP command will
				include a timestamp in its banner greeting.  The syntax of
				the timestamp corresponds to the `msg-id' in [RFC822], and
				MUST be different each time the POP3 server issues a banner
				greeting.
				
			Examples:
				S: +OK POP3 server ready <1896.697170952@dbc.mtview.ca.us>
				C: APOP mrose c4c9334bac560ecc979e58001b3e22fb
				S: +OK maildrop has 1 message (369 octets)

				In this example, the shared  secret  is  the  string  `tan-
				staaf'.  Hence, the MD5 algorithm is applied to the string

				<1896.697170952@dbc.mtview.ca.us>tanstaaf
				 
				which produces a digest value of
		            c4c9334bac560ecc979e58001b3e22fb
			 
			*/

			if(m_Authenticated){
				SendData("-ERR You are already authenticated\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There must be two params
			if(param.Length == 2){
				string userName   = param[0];
				string md5HexHash = param[1];

				// Check if user isn't logged in already
				if(m_pServer.IsUserLoggedIn(userName)){
					SendData("-ERR User:'" + userName + "' already logged in\r\n");
					return;
				}

				// Authenticate user
				if(m_pServer.OnAuthUser(this,userName,md5HexHash,m_MD5_prefix,AuthType.APOP)){
					m_UserName = userName;
					m_Authenticated = true;

					// Get user messages info.
					m_pServer.OnGetMessagesInfo(this,m_POP3_Messages);

					SendData("+OK authentication was successful\r\n");
				}
				else{
					SendData("-ERR authentication failed\r\n");
				}
			}
			else{
				SendData("-ERR syntax error. Syntax:{APOP userName md5HexHash}\r\n");
			}
		}

		#endregion

		#region function AUTH

		private void AUTH(string argsText)
		{
			/* Rfc 1734
				
				AUTH mechanism

					Arguments:
						a string identifying an IMAP4 authentication mechanism,
						such as defined by [IMAP4-AUTH].  Any use of the string
						"imap" used in a server authentication identity in the
						definition of an authentication mechanism is replaced with
						the string "pop".
						
					Possible Responses:
						+OK maildrop locked and ready
						-ERR authentication exchange failed

					Restrictions:
						may only be given in the AUTHORIZATION state

					Discussion:
						The AUTH command indicates an authentication mechanism to
						the server.  If the server supports the requested
						authentication mechanism, it performs an authentication
						protocol exchange to authenticate and identify the user.
						Optionally, it also negotiates a protection mechanism for
						subsequent protocol interactions.  If the requested
						authentication mechanism is not supported, the server						
						should reject the AUTH command by sending a negative
						response.

						The authentication protocol exchange consists of a series
						of server challenges and client answers that are specific
						to the authentication mechanism.  A server challenge,
						otherwise known as a ready response, is a line consisting
						of a "+" character followed by a single space and a BASE64
						encoded string.  The client answer consists of a line
						containing a BASE64 encoded string.  If the client wishes
						to cancel an authentication exchange, it should issue a
						line with a single "*".  If the server receives such an
						answer, it must reject the AUTH command by sending a
						negative response.

						A protection mechanism provides integrity and privacy
						protection to the protocol session.  If a protection
						mechanism is negotiated, it is applied to all subsequent
						data sent over the connection.  The protection mechanism
						takes effect immediately following the CRLF that concludes
						the authentication exchange for the client, and the CRLF of
						the positive response for the server.  Once the protection
						mechanism is in effect, the stream of command and response
						octets is processed into buffers of ciphertext.  Each
						buffer is transferred over the connection as a stream of
						octets prepended with a four octet field in network byte
						order that represents the length of the following data.
						The maximum ciphertext buffer length is defined by the
						protection mechanism.

						The server is not required to support any particular
						authentication mechanism, nor are authentication mechanisms
						required to support any protection mechanisms.  If an AUTH
						command fails with a negative response, the session remains
						in the AUTHORIZATION state and client may try another
						authentication mechanism by issuing another AUTH command,
						or may attempt to authenticate by using the USER/PASS or
						APOP commands.  In other words, the client may request
						authentication types in decreasing order of preference,
						with the USER/PASS or APOP command as a last resort.

						Should the client successfully complete the authentication
						exchange, the POP3 server issues a positive response and
						the POP3 session enters the TRANSACTION state.
						
				Examples:
							S: +OK POP3 server ready
							C: AUTH KERBEROS_V4
							S: + AmFYig==
							C: BAcAQU5EUkVXLkNNVS5FRFUAOCAsho84kLN3/IJmrMG+25a4DT
								+nZImJjnTNHJUtxAA+o0KPKfHEcAFs9a3CL5Oebe/ydHJUwYFd
								WwuQ1MWiy6IesKvjL5rL9WjXUb9MwT9bpObYLGOKi1Qh
							S: + or//EoAADZI=
							C: DiAF5A4gA+oOIALuBkAAmw==
							S: +OK Kerberos V4 authentication successful
								...
							C: AUTH FOOBAR
							S: -ERR Unrecognized authentication type
			 
			*/
			if(m_Authenticated){
				SendData("-ERR already authenticated\r\n");
				return;
			}
			
				
			//------ Parse parameters -------------------------------------//
			string userName = "";
			string password = "";

			string[] param = argsText.Split(new char[]{' '});
			switch(param[0].ToUpper())
			{
				case "PLAIN":
					SendData("-ERR Unrecognized authentication type.\r\n");
					break;

				case "LOGIN":

					#region LOGIN authentication

				    //---- AUTH = LOGIN ------------------------------
					/* Login
					C: AUTH LOGIN-MD5
					S: + VXNlcm5hbWU6
					C: username_in_base64
					S: + UGFzc3dvcmQ6
					C: password_in_base64
					
					   VXNlcm5hbWU6 base64_decoded= USERNAME
					   UGFzc3dvcmQ6 base64_decoded= PASSWORD
					*/
					// Note: all strings are base64 strings eg. VXNlcm5hbWU6 = UserName.
			
					
					// Query UserName
					SendData("+ VXNlcm5hbWU6\r\n");

					string userNameLine = ReadLine();
					// Encode username from base64
					if(userNameLine.Length > 0){
						userName = System.Text.Encoding.Default.GetString(Convert.FromBase64String(userNameLine));
					}
						
					// Query Password
					SendData("+ UGFzc3dvcmQ6\r\n");

					string passwordLine = ReadLine();
					// Encode password from base64
					if(passwordLine.Length > 0){
						password = System.Text.Encoding.Default.GetString(Convert.FromBase64String(passwordLine));
					}
																							
					if(m_pServer.OnAuthUser(this,userName,password,"",AuthType.Plain)){
						SendData("+OK Authentication successful.\r\n");
						
						m_UserName = userName;
						m_Authenticated = true;

						// Get user messages info.
						m_pServer.OnGetMessagesInfo(this,m_POP3_Messages);
					}
					else{
						SendData("-ERR Authentication failed\r\n");
					}

					#endregion

					break;

				case "CRAM-MD5":
					
					#region CRAM-MD5 authentication

					/* Cram-M5
					C: AUTH CRAM-MD5
					S: + <md5_calculation_hash_in_base64>
					C: base64(decoded:username password_hash)
					*/
					
					string md5Hash = "<" + Guid.NewGuid().ToString().ToLower() + ">";
					SendData("+ " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(md5Hash)) + "\r\n");

					string reply = this.ReadLine();

					reply = System.Text.Encoding.Default.GetString(Convert.FromBase64String(reply));
					string[] replyArgs = reply.Split(' ');
					userName = replyArgs[0];
					
					if(m_pServer.OnAuthUser(this,userName,replyArgs[1],md5Hash,AuthType.CRAM_MD5)){
						SendData("+OK Authentication successful.\r\n");
						
						m_UserName = userName;
						m_Authenticated = true;

						// Get user messages info.
						m_pServer.OnGetMessagesInfo(this,m_POP3_Messages);
					}
					else{
						SendData("-ERR Authentication failed\r\n");
					}

					#endregion

					break;

				case "DIGEST-MD5":

					/*	string md5Hash1 = "<" + Guid.NewGuid().ToString().ToLower() + ">";
						SendData("334 " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(md5Hash1)) + "\r\n");

						string reply1 = ReadLine();

						reply1 = System.Text.Encoding.Default.GetString(Convert.FromBase64String(reply1));
						m_pLogWriter.AddEntry(reply1 + "<CRLF>",this.SessionID,m_ConnectedIp,"C");
*/
					// ToDo: can't find any examples ???
					SendData("-ERR Unrecognized authentication type.\r\n");
					break;

				default:
					SendData("-ERR Unrecognized authentication type.\r\n");
					break;
			}
			//-----------------------------------------------------------------//
		}

		#endregion
				

		#region function Reset

		private void Reset()
		{		
			/* RFC 1939 5. RSET
			Discussion:
				If any messages have been marked as deleted by the POP3
				server, they are unmarked.
			*/
			m_POP3_Messages.ResetDeleteFlags();
		}

		#endregion

		#region function Update

		private void Update()
		{
			/* RFC 1939 6.
			NOTE:
				When the client issues the QUIT command from the TRANSACTION state,
				the POP3 session enters the UPDATE state.  (Note that if the client
				issues the QUIT command from the AUTHORIZATION state, the POP3
				session terminates but does NOT enter the UPDATE state.)

				If a session terminates for some reason other than a client-issued
				QUIT command, the POP3 session does NOT enter the UPDATE state and
				MUST not remove any messages from the maildrop.
			*/

			if(m_Authenticated){

				// Delete all message which are marked for deletion ---//
				foreach(POP3_Message msg in m_POP3_Messages.Messages){
					if(msg.MarkedForDelete){
						m_pServer.OnDeleteMessage(this,msg);
					}
				}
				//-----------------------------------------------------//
			}
		}

		#endregion


		#region function SendData
			
		/// <summary>
		/// Sends data synchronously.
		/// </summary>
		/// <param name="data"></param>
		private void SendData(string data)
		{
			byte[] byte_data = System.Text.Encoding.ASCII.GetBytes(data);
			int size = m_pSocket.Send(byte_data);

			if(m_pServer.LogCommands){
				string reply = System.Text.Encoding.ASCII.GetString(byte_data);
				reply = reply.Replace("\r\n","<CRLF>");
				m_pLogWriter.AddEntry(reply,this.SessionID,this.RemoteEndPoint.Address.ToString(),"S");
			}
		}

		/// <summary>
		/// Starts asynchronous sending.
		/// </summary>
		/// <param name="strm"></param>
		private void BeginSend(MemoryStream strm)
		{
			AsyncSocketHelper.BeginSend(m_pSocket,strm,null,new SocketCallBack(this.EndSend));

			if(m_pServer.LogCommands){
				m_pLogWriter.AddEntry("big binary " + strm.Length.ToString() + " bytes",this.SessionID,this.RemoteEndPoint.Address.ToString(),"S");
			}
		}

		/// <summary>
		/// Is called when asynchronous send completes.
		/// </summary>
		/// <param name="result">If true, then send was successfull.</param>
		/// <param name="count">Count sended.</param>
		/// <param name="exception">Exception happend on send. NOTE: available only is result=false.</param>
		/// <param name="tag">User data.</param>
		private void EndSend(SocketCallBackResult result,long count,Exception exception,object tag)
		{
			try{
				switch(result)
				{
					case SocketCallBackResult.Ok:
						BeginRecieveCmd();
						break;

					case SocketCallBackResult.SocketClosed:
						EndSession();
						break;

					case SocketCallBackResult.Exception:
						OnError(exception);
						break;
				}
			}
			catch(Exception x){
				OnError(x);
			}
		}

		#endregion		

		#region function ReadLine

		/// <summary>
		/// Reads line from socket.
		/// </summary>
		/// <returns></returns>
		private string ReadLine()
		{
			string line = Core.ReadLine(m_pSocket,500,m_pServer.SessionIdleTimeOut);
				
			if(m_pServer.LogCommands){
				m_pLogWriter.AddEntry(line + "<CRLF>",this.SessionID,this.RemoteEndPoint.Address.ToString(),"C");
			}

			return line;
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Gets session ID.
		/// </summary>
		public string SessionID
		{
			get{ return m_SessionID; }
		}

		/// <summary>
		/// Gets session start time.
		/// </summary>
		public DateTime SessionStartTime
		{
			get{ return m_SessionStartTime; }
		}

		/// <summary>
		/// Gets last data activity time.
		/// </summary>
		public DateTime SessionLastDataTime
		{
			get{ return m_LastDataTime; }
		}

		/// <summary>
		/// Gets loggded in user name (session owner).
		/// </summary>
		public string UserName
		{
			get{ return m_UserName; }
		}

		/// <summary>
		/// Gets EndPoint which accepted conection.
		/// </summary>
		public IPEndPoint LocalEndPoint
		{
			get{ return (IPEndPoint)m_pSocket.LocalEndPoint; }
		}

		/// <summary>
		/// Gets connected Host(client) EndPoint.
		/// </summary>
		public IPEndPoint RemoteEndPoint
		{
			get{ return (IPEndPoint)m_pSocket.RemoteEndPoint; }
		}
		
		/// <summary>
		/// Gets or sets custom user data.
		/// </summary>
		public object Tag
		{
			get{ return m_Tag; }

			set{ m_Tag = value; }
		}

		#endregion
		
	}
}
