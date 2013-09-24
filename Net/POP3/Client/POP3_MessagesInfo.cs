using System;
using System.Collections;

namespace LumiSoft.Net.POP3.Client
{
	/// <summary>
	/// Holds POP3 messages info.
	/// </summary>
	public class POP3_MessagesInfo
	{
		private Hashtable m_pMessages = null;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public POP3_MessagesInfo()
		{	
			m_pMessages = new Hashtable();
		}

		#region function Add

		internal void Add(string messageID,int messageNr,long messageSize)
		{
			m_pMessages.Add(messageNr,new POP3_MessageInfo(messageID,messageNr,messageSize));
		}

		#endregion


		#region function GetMessageInfo

		/// <summary>
		/// Gets specified message info.
		/// </summary>
		/// <param name="nr"></param>
		/// <returns></returns>
		public POP3_MessageInfo GetMessageInfo(int nr)
		{
			if(m_pMessages.ContainsKey(nr)){
				return (POP3_MessageInfo)m_pMessages[nr];
			}
			else{
				throw new Exception("No such message !");
			}
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Gets total size of messages.
		/// </summary>
		public long TotalSize
		{
			get{ return 0; }
		}

		/// <summary>
		/// Gets messages count.
		/// </summary>
		public int Count
		{
			get{ return m_pMessages.Count; }
		}

		/// <summary>
		/// Gets list of messages.
		/// </summary>
		public POP3_MessageInfo[] Messages
		{
			get{
				POP3_MessageInfo[] retVal = new POP3_MessageInfo[m_pMessages.Count];
				m_pMessages.Values.CopyTo(retVal,0);

				return retVal; 
			}
		}

		#endregion

	}
}
