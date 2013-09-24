using System;
using System.IO;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using LumiSoft.Net;

namespace LumiSoft.Net.IMAP.Server
{
	#region Event delegates

	/// <summary>
	/// Represents the method that will handle the AuthUser event for SMTP_Server.
	/// </summary>
	/// <param name="sender">The source of the event. </param>
	/// <param name="e">A AuthUser_EventArgs that contains the event data.</param>
	public delegate void AuthUserEventHandler(object sender,AuthUser_EventArgs e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void FolderEventHandler(object sender,Mailbox_EventArgs e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void FoldersEventHandler(object sender,IMAP_Folders e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void MessagesEventHandler(object sender,IMAP_Messages e);

	/// <summary>
	/// 
	/// </summary>
	public delegate void MessageEventHandler(object sender,Message_EventArgs e);

	#endregion

	/// <summary>
	/// IMAP server componet.
	/// </summary>
	public class IMAP_Server : SocketServer
	{
		private int m_MaxMessageSize = 1000000;

		#region Events declarations

		/// <summary>
		/// Occurs when new computer connected to IMAP server.
		/// </summary>
		public event ValidateIPHandler ValidateIPAddress = null;

		/// <summary>
		/// Occurs when connected user tryes to authenticate.
		/// </summary>
		public event AuthUserEventHandler AuthUser = null;

		/// <summary>
		/// Occurs when server requests to subscribe folder.
		/// </summary>
		public event FolderEventHandler SubscribeFolder = null;

		/// <summary>
		/// Occurs when server requests to unsubscribe folder.
		/// </summary>
		public event FolderEventHandler UnSubscribeFolder = null;

		/// <summary>
		/// Occurs when server requests all available folders.
		/// </summary>
		public event FoldersEventHandler GetFolders = null;

		/// <summary>
		/// Occurs when server requests subscribed folders.
		/// </summary>
		public event FoldersEventHandler GetSubscribedFolders = null;

		/// <summary>
		/// Occurs when server requests to create folder.
		/// </summary>
		public event FolderEventHandler CreateFolder = null;

		/// <summary>
		/// Occurs when server requests to delete folder.
		/// </summary>
		public event FolderEventHandler DeleteFolder = null;

		/// <summary>
		/// Occurs when server requests to rename folder.
		/// </summary>
		public event FolderEventHandler RenameFolder = null;

		/// <summary>
		/// Occurs when server requests to folder messages info.
		/// </summary>
		public event MessagesEventHandler GetMessagesInfo = null;

		/// <summary>
		/// Occurs when server requests to delete message.
		/// </summary>
		public event MessageEventHandler DeleteMessage = null;

		/// <summary>
		/// Occurs when server requests to store message.
		/// </summary>
		public event MessageEventHandler StoreMessage = null;

		/// <summary>
		/// Occurs when server requests to store message flags.
		/// </summary>
		public event MessageEventHandler StoreMessageFlags = null;

		/// <summary>
		/// Occurs when server requests to copy message to new location.
		/// </summary>
		public event MessageEventHandler CopyMessage = null;

		/// <summary>
		/// Occurs when server requests to get message.
		/// </summary>
		public event MessageEventHandler GetMessage = null;

		/// <summary>
		/// Occurs when IMAP session has finished and session log is available.
		/// </summary>
		public event LogEventHandler SessionLog = null;

		#endregion

		
		/// <summary>
		/// Defalut constructor.
		/// </summary>
		public IMAP_Server() : base()
		{
			IPEndPoint = new IPEndPoint(IPAddress.Any,143);
		}


		#region override InitNewSession

		/// <summary>
		/// 
		/// </summary>
		/// <param name="socket"></param>
		protected override void InitNewSession(Socket socket)
		{
			_LogWriter logWriter = new _LogWriter(this.SessionLog);
			IMAP_Session session = new IMAP_Session(socket,this,logWriter);
		}

		#endregion


		#region Properties Implementaion
				
		/// <summary>
		/// Maximum message size.
		/// </summary>
		public int MaxMessageSize 
		{
			get{ return m_MaxMessageSize; }

			set{ m_MaxMessageSize = value; }
		}
		
		#endregion

		#region Events Implementation

		#region function OnValidate_IpAddress
		
		/// <summary>
		/// Raises event ValidateIP.
		/// </summary>
		/// <param name="enpoint">Connected host EndPoint.</param>
		/// <returns>Returns true if connection allowed.</returns>
		internal bool OnValidate_IpAddress(IPEndPoint enpoint) 
		{			
			ValidateIP_EventArgs oArg = new ValidateIP_EventArgs(enpoint);
			if(this.ValidateIPAddress != null){
				this.ValidateIPAddress(this, oArg);
			}

			return oArg.Validated;						
		}

		#endregion

		#region function OnAuthUser

		/// <summary>
		/// Raises event AuthUser.
		/// </summary>
		/// <param name="session">Reference to current IMAP session.</param>
		/// <param name="userName">User name.</param>
		/// <param name="passwordData">Password compare data,it depends of authentication type.</param>
		/// <param name="data">For md5 eg. md5 calculation hash.It depends of authentication type.</param>
		/// <param name="authType">Authentication type.</param>
		/// <returns>Returns true if user is authenticated ok.</returns>
		internal bool OnAuthUser(IMAP_Session session,string userName,string passwordData,string data,AuthType authType)
		{
			AuthUser_EventArgs oArgs = new AuthUser_EventArgs(session,userName,passwordData,data,authType);
			if(this.AuthUser != null){
				this.AuthUser(this,oArgs);
			}

			return oArgs.Validated;
		}

		#endregion

		#region function OnSubscribeMailbox

		/// <summary>
		/// Raises event 'SubscribeMailbox'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox which to subscribe.</param>
		/// <returns></returns>
		internal string OnSubscribeMailbox(IMAP_Session session,string mailbox)
		{
			if(this.SubscribeFolder != null){
				Mailbox_EventArgs eArgs = new Mailbox_EventArgs(mailbox);
				this.SubscribeFolder(session,eArgs);

				return eArgs.ErrorText;
			}

			return null;
		}

		#endregion

		#region function OnUnSubscribeMailbox

		/// <summary>
		/// Raises event 'UnSubscribeMailbox'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox which to unsubscribe.</param>
		/// <returns></returns>
		internal string OnUnSubscribeMailbox(IMAP_Session session,string mailbox)
		{
			if(this.UnSubscribeFolder != null){
				Mailbox_EventArgs eArgs = new Mailbox_EventArgs(mailbox);
				this.UnSubscribeFolder(session,eArgs);

				return eArgs.ErrorText;
			}

			return null;
		}

		#endregion

		#region function OnGetSubscribedMailboxes

		/// <summary>
		/// Raises event 'GetSubscribedMailboxes'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="referenceName">Mailbox reference.</param>
		/// <param name="mailBox">Mailbox search pattern or mailbox.</param>
		/// <returns></returns>
		internal IMAP_Folders OnGetSubscribedMailboxes(IMAP_Session session,string referenceName,string mailBox)
		{
			IMAP_Folders retVal = new IMAP_Folders(referenceName,mailBox);
			if(this.GetSubscribedFolders != null){
				this.GetSubscribedFolders(session,retVal);
			}

			return retVal;
		}

		#endregion

		#region function OnGetMailboxes

		/// <summary>
		/// Raises event 'GetMailboxes'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="referenceName">Mailbox reference.</param>
		/// <param name="mailBox">Mailbox search pattern or mailbox.</param>
		/// <returns></returns>
		internal IMAP_Folders OnGetMailboxes(IMAP_Session session,string referenceName,string mailBox)
		{
			IMAP_Folders retVal = new IMAP_Folders(referenceName,mailBox);
			if(this.GetFolders != null){
				this.GetFolders(session,retVal);
			}

			return retVal;
		}

		#endregion

		#region function OnCreateMailbox

		/// <summary>
		/// Raises event 'CreateMailbox'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox to create.</param>
		/// <returns></returns>
		internal string OnCreateMailbox(IMAP_Session session,string mailbox)
		{
			if(this.CreateFolder != null){
				Mailbox_EventArgs eArgs = new Mailbox_EventArgs(mailbox);
				this.CreateFolder(session,eArgs);

				return eArgs.ErrorText;
			}

			return null;
		}

		#endregion

		#region function OnDeleteMailbox

		/// <summary>
		/// Raises event 'DeleteMailbox'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox which to delete.</param>
		/// <returns></returns>
		internal string OnDeleteMailbox(IMAP_Session session,string mailbox)
		{
			if(this.DeleteFolder != null){
				Mailbox_EventArgs eArgs = new Mailbox_EventArgs(mailbox);
				this.DeleteFolder(session,eArgs);

				return eArgs.ErrorText;
			}

			return null;
		}

		#endregion

		#region function OnRenameMailbox

		/// <summary>
		/// Raises event 'RenameMailbox'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox which to rename.</param>
		/// <param name="newMailboxName">New mailbox name.</param>
		/// <returns></returns>
		internal string OnRenameMailbox(IMAP_Session session,string mailbox,string newMailboxName)
		{
			if(this.RenameFolder != null){
				Mailbox_EventArgs eArgs = new Mailbox_EventArgs(mailbox,newMailboxName);
				this.RenameFolder(session,eArgs);

				return eArgs.ErrorText;
			}

			return null;
		}

		#endregion

		#region function OnGetMessagesInfo

		/// <summary>
		/// Raises event 'GetMessagesInfo'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="mailbox">Mailbox which messages info to get.</param>
		/// <returns></returns>
		internal IMAP_Messages OnGetMessagesInfo(IMAP_Session session,string mailbox)
		{
			IMAP_Messages messages = new IMAP_Messages(mailbox);
			if(this.GetMessagesInfo != null){
				this.GetMessagesInfo(session,messages);
			}

			return messages;
		}

		#endregion

		#region function OnGetMessage

		/// <summary>
		/// Raises event 'GetMessage'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="msg">Message which to get.</param>
		/// <param name="headersOnly">Specifies if message header or full message is wanted.</param>
		/// <returns></returns>
		internal Message_EventArgs OnGetMessage(IMAP_Session session,IMAP_Message msg,bool headersOnly)
		{
			Message_EventArgs eArgs = new Message_EventArgs(session.SelectedMailbox,msg,headersOnly);
			if(this.GetMessage != null){
				this.GetMessage(session,eArgs);
			}

			return eArgs;
		}

		#endregion

		#region function OnDeleteMessage

		/// <summary>
		/// Raises event 'DeleteMessage'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="message">Message which to delete.</param>
		/// <returns></returns>
		internal string OnDeleteMessage(IMAP_Session session,IMAP_Message message)
		{
			Message_EventArgs eArgs = new Message_EventArgs(session.SelectedMailbox,message);
			if(this.DeleteMessage != null){
				this.DeleteMessage(session,eArgs);
			}

			return eArgs.ErrorText;
		}

		#endregion

		#region function OnCopyMessage

		/// <summary>
		/// Raises event 'CopyMessage'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="msg">Message which to copy.</param>
		/// <param name="location">New message location.</param>
		/// <returns></returns>
		internal string OnCopyMessage(IMAP_Session session,IMAP_Message msg,string location)
		{
			Message_EventArgs eArgs = new Message_EventArgs(session.SelectedMailbox,msg,location);
			if(this.CopyMessage != null){
				this.CopyMessage(session,eArgs);
			}

			return eArgs.ErrorText;
		}

		#endregion

		#region function OnStoreMessage

		/// <summary>
		/// Raises event 'StoreMessage'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="folder">Folder where to store.</param>
		/// <param name="msg">Message which to store.</param>
		/// <param name="messageData">Message data which to store.</param>
		/// <returns></returns>
		internal string OnStoreMessage(IMAP_Session session,string folder,IMAP_Message msg,byte[] messageData)
		{
			Message_EventArgs eArgs = new Message_EventArgs(folder,msg);
			eArgs.MessageData = messageData;
			if(this.StoreMessage != null){
				this.StoreMessage(session,eArgs);
			}

			return eArgs.ErrorText;
		}

		#endregion

		#region function OnStoreMessageFlags

		/// <summary>
		/// Raises event 'StoreMessageFlags'.
		/// </summary>
		/// <param name="session">Reference to IMAP session.</param>
		/// <param name="msg">Message which flags to store.</param>
		/// <returns></returns>
		internal string OnStoreMessageFlags(IMAP_Session session,IMAP_Message msg)
		{
			Message_EventArgs eArgs = new Message_EventArgs(session.SelectedMailbox,msg);
			if(this.StoreMessageFlags != null){
				this.StoreMessageFlags(session,eArgs);
			}

			return eArgs.ErrorText;
		}

		#endregion

		#endregion

	}
}
