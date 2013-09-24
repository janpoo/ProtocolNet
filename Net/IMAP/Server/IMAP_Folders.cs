using System;
using System.Collections;

namespace LumiSoft.Net.IMAP.Server
{	
	/// <summary>
	/// IMAP folders collection.
	/// </summary>
	public class IMAP_Folders
	{
		private ArrayList m_Mailboxes = null;
		private string    m_RefName   = "";
		private string    m_Mailbox   = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <param name="folder"></param>
		public IMAP_Folders(string referenceName,string folder)
		{			
			m_Mailboxes = new ArrayList();
			m_RefName   = referenceName;
			m_Mailbox   = folder;
		}

		/// <summary>
		/// Adds folder to folders list.
		/// </summary>
		/// <param name="folder">Full path to folder, path separator = '/'. Eg. Inbox/myFolder .</param>
		public void Add(string folder)
		{
			if(m_RefName.Length > 0){
				// Check if starts with reference name
				if(!folder.ToLower().StartsWith(m_RefName.ToLower())){
					return;
				}
			}

			// ToDo: mailboxName *, %

			// Mailbox wildchar handling.
			// * - ALL   % - won't take sub folders, only current
			// * *mailbox* *mailbox% *mailbox mailbox* mailbox%
		/*	if(m_Mailbox == "*"){
				m_Mailboxes.Add(new Mailbox(mailbox));
				return;
			}*/
			if(m_Mailbox.StartsWith("*") && m_Mailbox.EndsWith("*")){
				if(folder.ToLower().IndexOf(m_Mailbox.Replace("*","").ToLower()) > -1){
					m_Mailboxes.Add(new IMAP_Folder(folder));
				}
				return;
			}

			// Eg. "INBOX", exact mailbox wanted.
			if(m_Mailbox.IndexOf("*") == -1 && m_Mailbox.IndexOf("%") == -1 && m_Mailbox.ToLower() != folder.ToLower()){				
				return;
			}

		// ??? is this needed
		//	if(m_Mailbox.StartsWith("*") && m_Mailbox.EndsWith("%")){
		//	}
	/*		if(m_Mailbox.StartsWith("*")){
				if(mailbox.ToLower().EndsWith(m_Mailbox.Replace("*","").ToLower())){
					m_Mailboxes.Add(new Mailbox(mailbox));
				}

				return;
			}
			if(m_Mailbox.EndsWith("*")){
				if(mailbox.ToLower().StartsWith(m_RefName + m_Mailbox.Replace("*","").ToLower())){
					m_Mailboxes.Add(new Mailbox(mailbox));
				}

				return;
			}
			if(m_Mailbox.EndsWith("%")){ 
				int nLastDirSep = ((string)(m_RefName + m_Mailbox.Replace("%",""))).LastIndexOf("/");
				if(mailbox.ToLower().StartsWith(m_RefName + m_Mailbox.Replace("%","").ToLower())){
					if(nLastDirSep != mailbox.LastIndexOf("/")){
						m_Mailboxes.Add(new Mailbox(mailbox));
					}
				}

				return;
			}*/
			
			m_Mailboxes.Add(new IMAP_Folder(folder));
		}

		/// <summary>
		/// 
		/// </summary>
		public IMAP_Folder[] Folders
		{
			get{ 
				IMAP_Folder[] retVal = new IMAP_Folder[m_Mailboxes.Count];
				m_Mailboxes.CopyTo(retVal);
				return retVal; 
			}
		}
	}
}
