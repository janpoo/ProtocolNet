using System;
using System.Net;

namespace LumiSoft.Net
{
	/// <summary>
	/// Provides data for the ValidateIPAddress event for servers.
	/// </summary>
	public class ValidateIP_EventArgs
	{
		private string m_ConnectedIP = "";
		private bool   m_Validated   = true;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="enpoint">Connected host EndPoint.</param>
		public ValidateIP_EventArgs(IPEndPoint enpoint)
		{
			m_ConnectedIP = enpoint.Address.ToString();
		}

		#region Properties Implementation

		/// <summary>
		/// IP address of computer, which is sending mail to here.
		/// </summary>
		public string ConnectedIP
		{
			get{ return m_ConnectedIP; }
		}

		/// <summary>
		/// Gets or sets if IP is allowed access.
		/// </summary>
		public bool Validated
		{
			get{ return m_Validated; }

			set{ m_Validated = value; }
		}

		#endregion

	}
}
