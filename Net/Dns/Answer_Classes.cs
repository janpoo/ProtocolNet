using System;

namespace LumiSoft.Net.Dns
{
	#region class A_Record

	/// <summary>
	/// A record class.
	/// </summary>
	public class A_Record
	{
		private string m_IP = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="IP">IP address.</param>
		public A_Record(string IP)
		{
			m_IP = IP;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets mail host dns name.
		/// </summary>
		public string IP
		{
			get{ return m_IP; }
		}

		#endregion

	}

	#endregion

	#region class NS_Record

	/// <summary>
	/// NS record class.
	/// </summary>
	public class NS_Record
	{
		private string m_NameServer = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="nameServer">Name server name.</param>
		public NS_Record(string nameServer)
		{
			m_NameServer = nameServer;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets name server name.
		/// </summary>
		public string NameServer
		{
			get{ return m_NameServer; }
		}

		#endregion

	}

	#endregion

	#region class NS_Record

	/// <summary>
	/// NS record class.
	/// </summary>
	public class CNAME_Record
	{
		private string m_Alias = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="nameServer">Alias.</param>
		public CNAME_Record(string alias)
		{
			m_Alias = alias;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets alias.
		/// </summary>
		public string Alias
		{
			get{ return m_Alias; }
		}

		#endregion

	}

	#endregion

	#region class PTR_Record

	/// <summary>
	/// PTR record class.
	/// </summary>
	public class PTR_Record
	{
		private string m_DomainName = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="domainName">DomainName.</param>
		public PTR_Record(string domainName)
		{
			m_DomainName = domainName;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets domain name.
		/// </summary>
		public string DomainName
		{
			get{ return m_DomainName; }
		}

		#endregion

	}

	#endregion

	#region class MX_Record

	/// <summary>
	/// MX record class.
	/// </summary>
	public class MX_Record
	{
		private int    m_Preference = 0;
		private string m_Host       = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="preference">MX record preference.</param>
		/// <param name="host">Mail host dns name.</param>
		public MX_Record(int preference,string host)
		{
			m_Preference = preference;
			m_Host       = host;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets MX record preference.
		/// </summary>
		public int Preference
		{
			get{ return m_Preference; }
		}

		/// <summary>
		/// Gets mail host dns name.
		/// </summary>
		public string Host
		{
			get{ return m_Host; }
		}

		#endregion

	}

	#endregion

	#region class TXT_Record

	/// <summary>
	/// TXT record class.
	/// </summary>
	public class TXT_Record
	{
		private string m_Text = "";

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="text">Text.</param>
		public TXT_Record(string text)
		{
			m_Text = text;
		}

		#region Properties Implementation

		/// <summary>
		/// Gets text.
		/// </summary>
		public string Text
		{
			get{ return m_Text; }
		}

		#endregion
	}

	#endregion
}
