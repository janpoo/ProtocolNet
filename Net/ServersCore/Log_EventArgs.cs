using System;

namespace LumiSoft.Net
{
	/// <summary>
	/// Provides data for the SessionLog event for POP3_Server and SMTP_Server.
	/// </summary>
	public class Log_EventArgs
	{
		private string m_LogText = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="logText"></param>
		public Log_EventArgs(string logText)
		{	
			m_LogText = logText;
		}


		#region Properties Implementation

		/// <summary>
		/// Gets log text.
		/// </summary>
		public string LogText
		{
			get{ return m_LogText; }
		}

		#endregion

	}
}
