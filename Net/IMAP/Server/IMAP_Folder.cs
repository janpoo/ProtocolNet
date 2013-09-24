using System;

namespace LumiSoft.Net.IMAP.Server
{
	/// <summary>
	/// IMAP mailbox
	/// </summary>
	public class IMAP_Folder
	{
		private string m_Folder = "";

		/// <summary>
		/// Default cnstructor.
		/// </summary>
		/// <param name="folder">Full path to folder, path separator = '/'. Eg. Inbox/myFolder .</param>
		public IMAP_Folder(string folder)
		{
			m_Folder = folder;
		}


		#region Properties Implementation

		/// <summary>
		/// IMAP folder.
		/// </summary>
		public string Folder
		{
			get{ return m_Folder; }
		}

		#endregion

	}
}
