using System;
using System.IO;
using System.Data;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LumiSoft.Net.FTP.Server
{
	#region Event delegates

	/// <summary>
	/// Represents the method that will handle the AuthUser event for FTP_Server.
	/// </summary>
	/// <param name="sender">The source of the event. </param>
	/// <param name="e">A AuthUser_EventArgs that contains the event data.</param>
	public delegate void AuthUserEventHandler(object sender,AuthUser_EventArgs e);


	/// <summary>
	/// Represents the method that will handle the filsystem rerlated events for FTP_Server.
	/// </summary>
	public delegate void FileSysEntryEventHandler(object sender,FileSysEntry_EventArgs e);

	#endregion

	/// <summary>
	/// FTP Server component.
	/// </summary>
	public class FTP_Server  : SocketServer
	{

		#region Event declarations

		/// <summary>
		/// Occurs when new computer connected to FTP server.
		/// </summary>
		public event ValidateIPHandler ValidateIPAddress = null;

		/// <summary>
		/// Occurs when connected user tryes to authenticate.
		/// </summary>
		public event AuthUserEventHandler AuthUser = null;

		/// <summary>
		/// Occurs when server needs directory info (directories,files in deirectory).
		/// </summary>
		public event FileSysEntryEventHandler GetDirInfo = null;

		/// <summary>
		/// Occurs when server needs to validatee directory.
		/// </summary>
		public event FileSysEntryEventHandler DirExists = null;

		/// <summary>
		/// Occurs when server needs needs to create directory.
		/// </summary>
		public event FileSysEntryEventHandler CreateDir = null;

		/// <summary>
		/// Occurs when server needs needs to delete directory.
		/// </summary>
		public event FileSysEntryEventHandler DeleteDir = null;

		/// <summary>
		/// Occurs when server needs needs validate file.
		/// </summary>
		public event FileSysEntryEventHandler FileExists = null;

		/// <summary>
		/// Occurs when server needs needs to store file.
		/// </summary>
		public event FileSysEntryEventHandler StoreFile = null;

		/// <summary>
		/// Occurs when server needs needs to get file.
		/// </summary>
		public event FileSysEntryEventHandler GetFile = null;

		/// <summary>
		/// Occurs when server needs needs to delete file.
		/// </summary>
		public event FileSysEntryEventHandler DeleteFile = null;

		/// <summary>
		/// Occurs when server needs needs to rname directory or file.
		/// </summary>
		public event FileSysEntryEventHandler RenameDirFile = null;

		/// <summary>
		/// Occurs when POP3 session has finished and session log is available.
		/// </summary>
		public event LogEventHandler SessionLog = null;

		#endregion


		/// <summary>
		/// Defalut constructor.
		/// </summary>
		public FTP_Server() : base()
		{
			IPEndPoint = new IPEndPoint(IPAddress.Any,21);
		}


		#region override InitNewSession

		/// <summary>
		/// 
		/// </summary>
		/// <param name="socket"></param>
		protected override void InitNewSession(Socket socket)
		{
			_LogWriter logWriter = new _LogWriter(this.SessionLog);
			FTP_Session session = new FTP_Session(socket,this,Guid.NewGuid().ToString(),logWriter);
		}

		#endregion


		#region Properties implementation

				
		#endregion

		#region Events Implementation

		#region function OnValidate_IpAddress

		/// <summary>
		/// Raises event ValidateIP.
		/// </summary>
		/// <param name="endpoint">Connected host EndPoint.</param>
		/// <returns></returns>
		internal virtual bool OnValidate_IpAddress(IPEndPoint endpoint) 
		{			
			ValidateIP_EventArgs oArg = new ValidateIP_EventArgs(endpoint);
			if(this.ValidateIPAddress != null){
				this.ValidateIPAddress(this, oArg);
			}

			return oArg.Validated;						
		}

		#endregion

		#region function OnAuthUser

		/// <summary>
		/// Authenticates user.
		/// </summary>
		/// <param name="session">Reference to current pop3 session.</param>
		/// <param name="userName">User name.</param>
		/// <param name="passwData"></param>
		/// <param name="data"></param>
		/// <param name="authType"></param>
		/// <returns></returns>
		internal virtual bool OnAuthUser(FTP_Session session,string userName,string passwData,string data,AuthType authType) 
		{				
			AuthUser_EventArgs oArg = new AuthUser_EventArgs(session,userName,passwData,data,authType);
			if(this.AuthUser != null){
				this.AuthUser(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion


		#region method OnGetDirInfo

		internal FileSysEntry_EventArgs OnGetDirInfo(string dir)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(dir,"");
			if(this.GetDirInfo != null){
				this.GetDirInfo(this,oArg);
			}
			return oArg;
		}

		#endregion

		#region method OnDirExists

		internal bool OnDirExists(string dir)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(dir,"");
			if(this.DirExists != null){
				this.DirExists(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion

		#region method OnCreateDir

		internal bool OnCreateDir(string dir)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(dir,"");
			if(this.CreateDir != null){
				this.CreateDir(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion

		#region method OnDeleteDir

		internal bool OnDeleteDir(string dir)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(dir,"");
			if(this.DeleteDir != null){
				this.DeleteDir(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion
		
		#region method OnRenameDirFile

		internal bool OnRenameDirFile(string from,string to)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(from,to);
			if(this.RenameDirFile != null){
				this.RenameDirFile(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion


		#region method OnFileExists

		internal bool OnFileExists(string file)
		{
			// Remove last /
			file = file.Substring(0,file.Length - 1);

			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(file,"");
			if(this.FileExists != null){
				this.FileExists(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion

		#region method OnGetFile

		internal Stream OnGetFile(string file)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(file,"");
			if(this.GetFile != null){
				this.GetFile(this,oArg);
			}
			
			return oArg.FileStream;
		}

		#endregion

		#region method OnStoreFile

		internal Stream OnStoreFile(string file)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(file,"");
			if(this.StoreFile != null){
				this.StoreFile(this,oArg);
			}
			
			return oArg.FileStream;
		}

		#endregion

		#region method OnDeleteFile

		internal bool OnDeleteFile(string file)
		{
			FileSysEntry_EventArgs oArg = new FileSysEntry_EventArgs(file,"");
			if(this.DeleteFile != null){
				this.DeleteFile(this,oArg);
			}
			
			return oArg.Validated;
		}

		#endregion
	
		#endregion
	}
}
