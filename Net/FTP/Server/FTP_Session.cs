using System;
using System.IO;
using System.Data;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LumiSoft.Net;

namespace LumiSoft.Net.FTP.Server
{
	/// <summary>
	/// FTP Session.
	/// </summary>
	public class FTP_Session : ISocketServerSession
	{
		private BufferedSocket m_pSocket    = null;  // Referance to client Socket.
		private FTP_Server  m_pServer          = null;  // Referance to POP3 server.
		private string      m_SessionID        = "";    // Holds session ID.
		private string      m_UserName         = "";    // Holds loggedIn UserName.
		private string      m_CurrentDir       = "/";
		private string      m_RenameFrom       = "";
		private bool        m_PassiveMode      = false;
		private TcpListener m_pPassiveListener = null;
		private IPEndPoint  m_pDataConEndPoint = null;
		private bool        m_Authenticated    = false; // Holds authentication flag.
		private int         m_BadCmdCount      = 0;     // Holds number of bad commands.
		private DateTime    m_SessionStartTime;
		private DateTime    m_LastDataTime;
		private _LogWriter  m_pLogWriter       = null;
		private object      m_Tag              = null;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="clientSocket">Referance to socket.</param>
		/// <param name="server">Referance to FTP server.</param>
		/// <param name="sessionID">Session ID which is assigned to this session.</param>
		/// <param name="logWriter">Log writer.</param>
		public FTP_Session(Socket clientSocket,FTP_Server server,string sessionID,_LogWriter logWriter)
		{
			m_pSocket    = new BufferedSocket(clientSocket);
			m_pServer          = server;
			m_SessionID        = sessionID;
			m_pLogWriter       = logWriter;
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
	
			// Check if ip is allowed to connect this computer
			if(m_pServer.OnValidate_IpAddress(this.RemoteEndPoint)){
				// Notify that server is ready
                if (this.m_pServer.m_WelcomeMsg == "")
                {
                    SendData("220 " + m_pServer.HostName + " FTP server ready\r\n");
                }
                else
                {
                    SendData(this.m_pServer.m_WelcomeMsg);
                }

				BeginRecieveCmd();
			}
			else{
				EndSession();
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
				SendData("500 Session timeout, closing transmission channel\r\n");
			}
			catch{
			}

			EndSession();
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
			AsyncSocketHelper.BeginRecieve(m_pSocket,strm,1024,"\r\n","\r\n",strm,new SocketCallBack(this.EndRecieveCmd),new SocketActivityCallback(this.SocketActivityCallback));
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
						SendData("500 Line too long.\r\n");

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


		private void SocketActivityCallback(object tag)
		{
		}


		#region function SwitchCommand

		/// <summary>
		/// Parses and executes POP3 commmand.
		/// </summary>
		/// <param name="commandTxt">FTP command text.</param>
		/// <returns>Returns true,if session must be terminated.</returns>
		private bool SwitchCommand(string commandTxt)
		{
			//---- Parse command --------------------------------------------------//
			string[] cmdParts = commandTxt.TrimStart().Split(new char[]{' '});
			string   command  = cmdParts[0].ToUpper().Trim();
			string   argsText = Core.GetArgsText(commandTxt,command);
			//---------------------------------------------------------------------//


			/*
			USER <SP> <username> <CRLF>
            PASS <SP> <password> <CRLF>
            ACCT <SP> <account-information> <CRLF>
            CWD  <SP> <pathname> <CRLF>
            CDUP <CRLF>
            SMNT <SP> <pathname> <CRLF>
            QUIT <CRLF>
            REIN <CRLF>
            PORT <SP> <host-port> <CRLF>
            PASV <CRLF>
            TYPE <SP> <type-code> <CRLF>
            STRU <SP> <structure-code> <CRLF>
            MODE <SP> <mode-code> <CRLF>
            RETR <SP> <pathname> <CRLF>
            STOR <SP> <pathname> <CRLF>
            STOU <CRLF>
            APPE <SP> <pathname> <CRLF>
            ALLO <SP> <decimal-integer>
                [<SP> R <SP> <decimal-integer>] <CRLF>
            REST <SP> <marker> <CRLF>
            RNFR <SP> <pathname> <CRLF>
            RNTO <SP> <pathname> <CRLF>
            ABOR <CRLF>
            DELE <SP> <pathname> <CRLF>
            RMD  <SP> <pathname> <CRLF>
            MKD  <SP> <pathname> <CRLF>
            PWD  <CRLF>
            LIST [<SP> <pathname>] <CRLF>
            NLST [<SP> <pathname>] <CRLF>
            SITE <SP> <string> <CRLF>
            SYST <CRLF>
            STAT [<SP> <pathname>] <CRLF>
            HELP [<SP> <string>] <CRLF>
            NOOP <CRLF>
			*/ 
			

			switch(command)
			{
				case "USER":
					USER(argsText);
					break;
				
				case "PASS":
					PASS(argsText);
					break;

				case "CWD":
					CWD(argsText);
					break;

				case "CDUP":
					CDUP(argsText);
					break;

			//	case "REIN":
			//		break;

				case "QUIT":
					QUIT();
					return true;

				case "PORT":
					PORT(argsText);
					break;

				case "PASV":
					PASV(argsText);
					break;

				case "TYPE":
					TYPE(argsText);
					break;

				case "RETR":
					RETR(argsText);
					break;

				case "STOR":
					STOR(argsText);
					break;

			//	case "STOU":
			//		break;

				case "APPE":
					APPE(argsText);
					break;

			//	case "REST":
			//		break;

				case "RNFR":
					RNFR(argsText);
					break;

				case "RNTO":
					RNTO(argsText);
					break;

			//	case "ABOR":
			//		break;

				case "DELE":
					DELE(argsText);
					break;

				case "RMD":
					RMD(argsText);
					break;

				case "MKD":
					MKD(argsText);
					break;

				case "PWD":
					PWD();
					break;

				case "LIST":
					LIST(argsText);
					break;

				case "NLST":
					NLST(argsText);
					break;

				case "SYST":
					SYST();
					break;

			//	case "STAT":
			//		break;

			//	case "HELP":
			//		break;

				case "NOOP":
					NOOP();
					break;

				case "":
					break;
										
				default:					
					//SendData("500 Invalid command " + command + "\r\n");
                    SendData("530 Not logged in.\r\n");

					//---- Check that maximum bad commands count isn't exceeded ---------------//
					if(m_BadCmdCount > m_pServer.MaxBadCommands-1){
						SendData("421 Too many bad commands, closing transmission channel\r\n");
						return true;
					}
					m_BadCmdCount++;
					//-------------------------------------------------------------------------//

					break;				
			}

			BeginRecieveCmd();
			
			return false;
		}

		#endregion


		#region method USER

		private void USER(string argsText)
		{
			if(m_Authenticated){
				SendData("500 You are already authenticated\r\n");
				return;
			}
			if(m_UserName.Length > 0){
				SendData("500 username is already specified, please specify password\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There must be only one parameter - userName
			if(argsText.Length > 0 && param.Length == 1){
				string userName = param[0];
							
				SendData("331 Password required or user:'" + userName + "'\r\n");
				m_UserName = userName;
			}
			else{
				SendData("500 Syntax error. Syntax:{USER username}\r\n");
			}
		}

		#endregion

		#region method PASS

		private void PASS(string argsText)
		{
			if(m_Authenticated){
				SendData("500 You are already authenticated\r\n");
				return;
			}
			if(m_UserName.Length == 0){
				SendData("503 please specify username first\r\n");
				return;
			}

			string[] param = argsText.Split(new char[]{' '});

			// There may be only one parameter - password
			if(param.Length == 1){
				string password = param[0];
									
				// Authenticate user
				if(m_pServer.OnAuthUser(this,m_UserName,password,"",AuthType.Plain)){
					m_Authenticated = true;

					SendData("230 Password ok\r\n");
				}
				else{						
					SendData("530 Not logged in.\r\n");					
					m_UserName = ""; // Reset userName !!!
				}
			}
			else{
				SendData("500 Syntax error. Syntax:{PASS userName}\r\n");
			}
		}

		#endregion


		#region method CWD

		private void CWD(string argsText)
		{
			/*
				This command allows the user to work with a different
				directory or dataset for file storage or retrieval without
				altering his login or accounting information.  Transfer
				parameters are similarly unchanged.  The argument is a
				pathname specifying a directory or other system dependent
				file group designator.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}
		
			string dir = GetAndNormailizePath(argsText);
	
			//Check if dir exists and is accesssible for this user
			if(m_pServer.OnDirExists(dir)){
				m_CurrentDir = dir;

				SendData("250 CDW command successful.\r\n");
			}
			else{
				SendData("550 System can't find directory '" + dir + "'.\r\n");
			}
		}

		#endregion

		#region method CDUP

		private void CDUP(string argsText)
		{
			/*
				This command is a special case of CWD, and is included to
				simplify the implementation of programs for transferring
				directory trees between operating systems having different
				syntaxes for naming the parent directory.  The reply codes
				shall be identical to the reply codes of CWD.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			// Move dir up if possible
			string[] pathParts = m_CurrentDir.Split('/');
			if(pathParts.Length > 1){
				m_CurrentDir = "";
				for(int i=0;i<(pathParts.Length - 2);i++){
					m_CurrentDir += pathParts[i] + "/";
				}

				if(m_CurrentDir.Length == 0){
					m_CurrentDir = "/";
				}
			}

			SendData("250 CDUP command successful.\r\n");
		}

		#endregion

		#region method PWD

		private void PWD()
		{
			/*
				This command causes the name of the current working
				directory to be returned in the reply.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			SendData("257 \"" + m_CurrentDir + "\" is current directory.\r\n");
		}

		#endregion


		#region method RETR

		private void RETR(string argsText)
		{
			/*
				This command causes the server-DTP to transfer a copy of the
				file, specified in the pathname, to the server- or user-DTP
				at the other end of the data connection.  The status and
				contents of the file at the server site shall be unaffected.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			// ToDo: custom errors 
			//---- See if path accessible
			Stream fileStream = null;
			try{	
				string file = GetAndNormailizePath(argsText);
				file = file.Substring(0,file.Length - 1);

				fileStream = m_pServer.OnGetFile(file);
			}
			catch{
				SendData("550 Access denied or directory dosen't exist !\r\n");
				return;
			}

			Socket socket = GetDataConnection();
			if(socket == null){
				return;
			}

			try{
			//	string file = GetAndNormailizePath(argsText);
			//	file = file.Substring(0,file.Length - 1);

			//	using(Stream fileStream = m_pServer.OnGetFile(file)){
					if(fileStream != null){
						// ToDo: bandwidth limiting here

						int readed = 1;
						while(readed > 0){
							byte[] data = new byte[10000];
							readed = fileStream.Read(data,0,data.Length);
							socket.Send(data);
						}
					}
			//	}
				
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();

				SendData("226 Transfer Complete.\r\n");
			}
			catch{
				SendData("426 Connection closed; transfer aborted.\r\n");
			}

			fileStream.Close();
		}

		#endregion

		#region method STOR

		private void STOR(string argsText)
		{
			/*
				This command causes the server-DTP to transfer a copy of the
				file, specified in the pathname, to the server- or user-DTP
				at the other end of the data connection.  The status and
				contents of the file at the server site shall be unaffected.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}
		
			// ToDo: custom errors 
			//---- See if path accessible
			Stream fileStream = null;
			try{	
				string file = GetAndNormailizePath(argsText);
				file = file.Substring(0,file.Length - 1);

				fileStream = m_pServer.OnStoreFile(file);
			}
			catch{
				SendData("550 Access denied or directory dosen't exist !\r\n");
				return;
			}

			Socket socket = GetDataConnection();
			if(socket == null){
				return;
			}
			try{
				string file = GetAndNormailizePath(argsText);
				file = file.Substring(0,file.Length - 1);

			//	using(Stream fileStream = m_pServer.OnStoreFile(file)){
					if(fileStream != null){
						// ToDo: bandwidth limiting here

						int readed = 1;
						while(readed > 0){
							byte[] data = new byte[10000];
							readed = socket.Receive(data);
							fileStream.Write(data,0,readed);							
						}
					}
			//	}
				
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();

				SendData("226 Transfer Complete.\r\n");
			}
			catch{ // ToDo: report right errors here. eg. DataConnection read time out, ... .
				SendData("426 Connection closed; transfer aborted.\r\n");
			}

			fileStream.Close();
		}

		#endregion

		#region method DELE

		private void DELE(string argsText)
		{
			/*
				This command causes the file specified in the pathname to be
				deleted at the server site.  If an extra level of protection
				is desired (such as the query, "Do you really wish to
				delete?"), it should be provided by the user-FTP process.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			string file = GetAndNormailizePath(argsText);
			file = file.Substring(0,file.Length - 1);

			m_pServer.OnDeleteFile(file);

			SendData("250 file deleted.\r\n");
		}

		#endregion

		#region method APPE

		private void APPE(string argsText)
		{
			/*
				This command causes the server-DTP to accept the data
				transferred via the data connection and to store the data in
				a file at the server site.  If the file specified in the
				pathname exists at the server site, then the data shall be
				appended to that file; otherwise the file specified in the
				pathname shall be created at the server site.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			//m_pServer.OnDeleteFile();

			SendData("500 unimplemented\r\n");
		}

		#endregion


		#region method RNFR

		private void RNFR(string argsText)
		{
			/*
				This command specifies the old pathname of the file which is
				to be renamed.  This command must be immediately followed by
				a "rename to" command specifying the new file pathname.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			string dir = GetAndNormailizePath(argsText);

			if(m_pServer.OnDirExists(dir) || m_pServer.OnFileExists(dir)){
				SendData("350 Please specify destination name.\r\n");

				m_RenameFrom = dir;
			}
			else{
				SendData("550 File or directory doesn't exist.\r\n");
			}
		}

		#endregion

		#region method RNTO

		private void RNTO(string argsText)
		{
			/*
				This command specifies the new pathname of the file
				specified in the immediately preceding "rename from"
				command.  Together the two commands cause a file to be
				renamed.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}
			if(m_RenameFrom.Length == 0){
				SendData("503 Bad sequence of commands.\r\n");
				return;
			}

			string dir = GetAndNormailizePath(argsText);

			if(m_pServer.OnRenameDirFile(m_RenameFrom,dir)){
				SendData("250 Directory renamed.\r\n");

				m_RenameFrom = "";
			}
			else{
				SendData("550 Error renameing directory or file .\r\n");
			}
		}

		#endregion


		#region method RMD

		private void RMD(string argsText)
		{
			/*
				This command causes the directory specified in the pathname
				to be removed as a directory (if the pathname is absolute)
				or as a subdirectory of the current working directory (if
				the pathname is relative).
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			string dir = GetAndNormailizePath(argsText);

			if(m_pServer.OnDeleteDir(dir)){
				SendData("250 \"" + dir + "\" directory deleted.\r\n");
			}
			else{
				SendData("550 Directory deletion failed.\r\n");
			}
		}

		#endregion

		#region method MKD

		private void MKD(string argsText)
		{
			/*
				This command causes the directory specified in the pathname
				to be created as a directory (if the pathname is absolute)
				or as a subdirectory of the current working directory (if
				the pathname is relative).
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			string dir = GetAndNormailizePath(argsText);

			if(m_pServer.OnCreateDir(dir)){
				SendData("257 \"" + dir + "\" directory created.\r\n");
			}
			else{
				SendData("550 Directory creation failed.\r\n");
			}
		}

		#endregion

		#region method LIST
		
		private void LIST(string argsText)
		{
			/*
				This command causes a list to be sent from the server to the
				passive DTP.  If the pathname specifies a directory or other
				group of files, the server should transfer a list of files
				in the specified directory.  If the pathname specifies a
				file then the server should send current information on the
				file.  A null argument implies the user's current working or
				default directory.  The data transfer is over the data
				connection in type ASCII or type EBCDIC.  (The user must
				ensure that the TYPE is appropriately ASCII or EBCDIC).
				Since the information on a file may vary widely from system
				to system, this information may be hard to use automatically
				in a program, but may be quite useful to a human user.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			// ToDo: custom errors 
			//---- See if path accessible			
			FileSysEntry_EventArgs eArgs = m_pServer.OnGetDirInfo(GetAndNormailizePath(argsText));
			if(!eArgs.Validated){
				SendData("550 Access denied or directory dosen't exist !\r\n");
				return;
			}
			DataSet ds = eArgs.DirInfo;

			Socket socket = GetDataConnection();
			if(socket == null){
				return;
			}

			try{
			//	string dir = GetAndNormailizePath(argsText);
			//	DataSet ds = m_pServer.OnGetDirInfo(dir);
			
				foreach(DataRow dr in ds.Tables["DirInfo"].Rows){
					string   name  = dr["Name"].ToString();
					string   date  = Convert.ToDateTime(dr["Date"]).ToString("MM-dd-yy hh:mmtt");
					bool     isDir = Convert.ToBoolean(dr["IsDirectory"]);

					if(isDir){
						socket.Send(System.Text.Encoding.Default.GetBytes(date + " <DIR> " + name + "\r\n"));
					}
					else{
						socket.Send(System.Text.Encoding.Default.GetBytes(date + " " + dr["Size"].ToString() + " " + name + "\r\n"));
					}
				}
				
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();

				SendData("226 Transfer Complete.\r\n");
			}
			catch{
				SendData("426 Connection closed; transfer aborted.\r\n");
			}
		}

		#endregion

		#region method NLST

		private void NLST(string argsText)
		{
			/*
				This command causes a directory listing to be sent from
				server to user site.  The pathname should specify a
				directory or other system-specific file group descriptor; a
				null argument implies the current directory.  The server
				will return a stream of names of files and no other
				information.  The data will be transferred in ASCII or
				EBCDIC type over the data connection as valid pathname
				strings separated by <CRLF> or <NL>.  (Again the user must
				ensure that the TYPE is correct.)  This command is intended
				to return information that can be used by a program to
				further process the files automatically.  For example, in
				the implementation of a "multiple get" function.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			//---- See if path accessible
			FileSysEntry_EventArgs eArgs = m_pServer.OnGetDirInfo(GetAndNormailizePath(argsText));
			if(!eArgs.Validated){
				SendData("550 Access denied or directory dosen't exist !\r\n");
				return;
			}
			DataSet ds = eArgs.DirInfo;

			Socket socket = GetDataConnection();
			if(socket == null){
				return;
			}

			try{
		//		string dir = GetAndNormailizePath(argsText);
		//		DataSet ds = m_pServer.OnGetDirInfo(dir);
			
				foreach(DataRow dr in ds.Tables["DirInfo"].Rows){
					socket.Send(System.Text.Encoding.Default.GetBytes(dr["Name"].ToString() + "\r\n"));
				}
				socket.Send(System.Text.Encoding.Default.GetBytes("aaa\r\n"));

				socket.Shutdown(SocketShutdown.Both);
				socket.Close();

				SendData("226 Transfer Complete.\r\n");
			}
			catch{
				SendData("426 Connection closed; transfer aborted.\r\n");
			}
		}

		#endregion


		#region method TYPE

		private void TYPE(string argsText)
		{
			/*
				The argument specifies the representation type as described
				in the Section on Data Representation and Storage.  Several
				types take a second parameter.  The first parameter is
				denoted by a single Telnet character, as is the second
				Format parameter for ASCII and EBCDIC; the second parameter
				for local byte is a decimal integer to indicate Bytesize.
				The parameters are separated by a <SP> (Space, ASCII code
				32).

				The following codes are assigned for type:

							\    /
				A - ASCII |    | N - Non-print
							|-><-| T - Telnet format effectors
				E - EBCDIC|    | C - Carriage Control (ASA)
							/    \
				I - Image
	               
				L <byte size> - Local byte Byte size
				
				The default representation type is ASCII Non-print.  If the
				Format parameter is changed, and later just the first
				argument is changed, Format then returns to the Non-print
				default.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			if(argsText.Trim().ToUpper() == "A" || argsText.Trim().ToUpper() == "I"){
				SendData("200 Type is set to " + argsText + ".\r\n");
			}
			else{
				SendData("500 Invalid type " + argsText + " .\r\n");
			}
		}

		#endregion

		#region method PORT

		private void PORT(string argsText)
		{
			/*
				 The argument is a HOST-PORT specification for the data port
				to be used in data connection.  There are defaults for both
				the user and server data ports, and under normal
				circumstances this command and its reply are not needed.  If
				this command is used, the argument is the concatenation of a
				32-bit internet host address and a 16-bit TCP port address.
				This address information is broken into 8-bit fields and the
				value of each field is transmitted as a decimal number (in
				character string representation).  The fields are separated
				by commas.  A port command would be:

				PORT h1,h2,h3,h4,p1,p2

				where h1 is the high order 8 bits of the internet host
				address.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			string[] parts = argsText.Split(',');
			if(parts.Length != 6){
				SendData("550 Invalid arguments.\r\n");
				return;
			}

			string ip   = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];
			int    port = (Convert.ToInt32(parts[4]) << 8) | Convert.ToInt32(parts[5]);

			m_pDataConEndPoint = new IPEndPoint(System.Net.Dns.GetHostByAddress(ip).AddressList[0],port);

			SendData("200 PORT Command successful.\r\n");
		}

		#endregion

		#region method PASV

		private void PASV(string argsText)
		{
			/*
				This command requests the server-DTP to "listen" on a data
				port (which is not its default data port) and to wait for a
				connection rather than initiate one upon receipt of a
				transfer command.  The response to this command includes the
				host and port address this server is listening on.
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			//--- Find free port -------------------------------//
			int port = 6000;
			while(true){				
				try{
					m_pPassiveListener = new TcpListener(IPAddress.Any,port);	
					m_pPassiveListener.Start();

					// If we reach here then port is free
					break;
				}
				catch{
				}

				port++;
			}
			//--------------------------------------------------//

			// Notify client on what IP and port server is listening client to connect.
			// PORT h1,h2,h3,h4,p1,p2
			string ip = m_pSocket.LocalEndPoint.ToString();
			ip = ip.Substring(0,ip.IndexOf(":"));
			ip.Replace(".",",");
			ip += "," + (port >> 8) + "," + (port & 255);

			SendData("227 Entering Passive Mode (" + ip + ").\r\n");

			m_PassiveMode = true;
		}

		#endregion

		#region method SYST

		private void SYST()
		{
			/*
				This command is used to find out the type of operating
				system at the server.  The reply shall have as its first
				word one of the system names listed in the current version
				of the Assigned Numbers document [4].
			*/
			if(!m_Authenticated){
				SendData("530 Please authenticate firtst !\r\n");
				return;
			}

			SendData("215 Windows_NT\r\n");
		}

		#endregion


		#region method NOOP

		private void NOOP()
		{
			/*
				This command does not affect any parameters or previously
				entered commands. It specifies no action other than that the
				server send an OK reply.
			*/
			SendData("200 OK\r\n");
		}

		#endregion

		#region method QUIT

		private void QUIT()
		{
			/*
				This command terminates a USER and if file transfer is not
				in progress, the server closes the control connection.  If
				file transfer is in progress, the connection will remain
				open for result response and the server will then close it.
				If the user-process is transferring files for several USERs
				but does not wish to close and then reopen connections for
				each, then the REIN command should be used instead of QUIT.

				An unexpected close on the control connection will cause the
				server to take the effective action of an abort (ABOR) and a
				logout (QUIT).
			*/
			SendData("221 Goodbye, closing serssin.\r\n");
		}

		#endregion


		#region function SendData
			
		private void SendData(string data)
		{
			Byte[] byte_data = System.Text.Encoding.ASCII.GetBytes(data.ToCharArray());
			int size = m_pSocket.Send(byte_data);

			if(m_pServer.LogCommands){
				string reply = System.Text.Encoding.ASCII.GetString(byte_data);
				reply = reply.Replace("\r\n","<CRLF>");
				m_pLogWriter.AddEntry(reply,this.SessionID,this.RemoteEndPoint.Address.ToString(),"S");
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


		#region method GetAndNormailizePath

		private string GetAndNormailizePath(string path)
		{
			// If conatins \, replace with / 
			string dir = path.Replace("\\","/");

			// If doesn't end with /, append it
			if(!dir.EndsWith("/")){
				dir += "/";
			}

			// Current path + path wanted if path not starting /
			if(!path.StartsWith("/")){
				dir = m_CurrentDir + dir;
			}
			
			//--- Normalize path, eg. /ivx/../test must be /test  -----//
			ArrayList pathParts = new ArrayList();
			string[] p = dir.Split('/');
			pathParts.AddRange(p);		
			
			for(int i=0;i<pathParts.Count;i++){
				if(pathParts[i].ToString() == ".."){
					// Don't let go over root path, eg. /../ - there wanted directory upper root.
					// Just skip this movement.
					if(i > 0){
						pathParts.RemoveAt(i - 1);
						i--;
					}

					pathParts.RemoveAt(i);
					i--;
				}
			}

			dir = dir.Replace("//","/");

			return dir;
		}

		#endregion

		#region method GetDataConnection

		private Socket GetDataConnection()
		{
			Socket socket = null;
			try{
				if(m_PassiveMode){
					//--- Wait ftp client connection ---------------------------//			
					long startTime = DateTime.Now.Ticks;
					// Wait ftp server to connect
					while(!m_pPassiveListener.Pending()){
						System.Threading.Thread.Sleep(50);

						// Time out after 30 seconds
						if((DateTime.Now.Ticks - startTime) / 10000 > 20000){
							throw new Exception("Ftp server didn't respond !");
						}
					}
					//-----------------------------------------------------------//

					socket = m_pPassiveListener.AcceptSocket();

					SendData("125 Data connection open, Transfer starting.\r\n");
				}
				else{
					SendData("150 Opening data connection.\r\n");

					socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
					socket.Connect(m_pDataConEndPoint);
				}
			}
			catch{
				SendData("425 Can't open data connection.\r\n");
				return null;
			}

			m_PassiveMode = false;

			return socket;
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
