using System;
using System.IO;
using System.Text;
using System.Collections;

namespace LumiSoft.Net.Mime
{
	/// <summary>
	/// Mime constructor.
	/// </summary>
	public class MimeConstructor
	{
		private string      m_MsgID        = "";
		private string[]    m_To           = null;
		private string[]    m_Cc           = null;
		private string[]    m_Bcc          = null;
		private string      m_From         = "";
		private string      m_Subject      = "";
		private string      m_Body         = "";
		private string      m_BodyHtml     = "";
		private string      m_CharSet      = "";
		private DateTime    m_MsgDate;
		private Attachments m_pAttachments = null;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public MimeConstructor()
		{
			m_pAttachments = new Attachments();

			m_MsgDate = DateTime.Now;
			m_MsgID = "<" + Guid.NewGuid().ToString().Replace("-","") + ">";
			m_CharSet = "utf-8";
		}


		#region method ConstructBinaryMime

		/// <summary>
		/// Constructs mime message.
		/// </summary>
		/// <returns></returns>
		public MemoryStream ConstructBinaryMime()
		{
			MemoryStream mime = new MemoryStream();
			byte[]       buf  = null; 

			string mainBoundaryID = "----=_NextPart_" + Guid.NewGuid().ToString().Replace("-","_");

			// Message-ID:
			buf = System.Text.Encoding.Default.GetBytes("Message-ID: " + m_MsgID + "\r\n");
			mime.Write(buf,0,buf.Length);
			
			// From:
			buf = System.Text.Encoding.Default.GetBytes("From: " + Core.CanonicalEncode(m_From,m_CharSet) + "\r\n");
			mime.Write(buf,0,buf.Length);

			// To:
			buf = System.Text.Encoding.Default.GetBytes("To: " + ConstructAddress(m_To));
			mime.Write(buf,0,buf.Length);

			// Cc:
			if(m_Cc != null && m_Cc.Length > 0){
				buf = System.Text.Encoding.Default.GetBytes("Cc: " + ConstructAddress(m_Cc));
				mime.Write(buf,0,buf.Length);
			}

			// Bcc:
			if(m_Bcc != null && m_Bcc.Length > 0){
				buf = System.Text.Encoding.Default.GetBytes("Bcc: " + ConstructAddress(m_Bcc));
				mime.Write(buf,0,buf.Length);
			}

			// Subject:
			buf = System.Text.Encoding.Default.GetBytes("Subject: " + Core.CanonicalEncode(m_Subject,m_CharSet) + "\r\n");
			mime.Write(buf,0,buf.Length);

			// Date:
			buf = System.Text.Encoding.Default.GetBytes("Date: " + m_MsgDate.ToUniversalTime().ToString("r",System.Globalization.DateTimeFormatInfo.InvariantInfo) + "\r\n");
			mime.Write(buf,0,buf.Length);

			// MIME-Version:
			buf = System.Text.Encoding.Default.GetBytes("MIME-Version: 1.0\r\n");
			mime.Write(buf,0,buf.Length);

			// Content-Type:
			buf = System.Text.Encoding.Default.GetBytes("Content-Type: " + "multipart/mixed;\r\n\tboundary=\"" + mainBoundaryID + "\"\r\n");
			mime.Write(buf,0,buf.Length);

			// 
			buf = System.Text.Encoding.Default.GetBytes("\r\nThis is a multi-part message in MIME format.\r\n\r\n");
			mime.Write(buf,0,buf.Length);

			
			string bodyBoundaryID = "----=_NextPart_" + Guid.NewGuid().ToString().Replace("-","_");

			buf = System.Text.Encoding.Default.GetBytes("--" + mainBoundaryID + "\r\n");
			mime.Write(buf,0,buf.Length);
			buf = System.Text.Encoding.Default.GetBytes("Content-Type: multipart/alternative;\r\n\tboundary=\"" + bodyBoundaryID + "\"\r\n\r\n");
			mime.Write(buf,0,buf.Length);

			buf = System.Text.Encoding.Default.GetBytes(ConstructBody(bodyBoundaryID));
			mime.Write(buf,0,buf.Length);

			buf = System.Text.Encoding.Default.GetBytes("--" + bodyBoundaryID + "--\r\n");
			mime.Write(buf,0,buf.Length);

			//-- Construct attachments
			foreach(Attachment att in m_pAttachments){
				buf = System.Text.Encoding.Default.GetBytes("\r\n--" + mainBoundaryID + "\r\n");
				mime.Write(buf,0,buf.Length);

				buf = System.Text.Encoding.Default.GetBytes("Content-Type: application/octet;\r\n\tname=\"" + Core.CanonicalEncode(att.FileName,m_CharSet) + "\"\r\n");
				mime.Write(buf,0,buf.Length);

				buf = System.Text.Encoding.Default.GetBytes("Content-Transfer-Encoding: base64\r\n");
				mime.Write(buf,0,buf.Length);

				buf = System.Text.Encoding.Default.GetBytes("Content-Disposition: attachment;\r\n\tfilename=\"" + Core.CanonicalEncode(att.FileName,m_CharSet) + "\"\r\n\r\n");
				mime.Write(buf,0,buf.Length);
				
				buf = System.Text.Encoding.Default.GetBytes(SplitString(Convert.ToBase64String(att.FileData)));
				mime.Write(buf,0,buf.Length);
			}

			buf = System.Text.Encoding.Default.GetBytes("\r\n");
			mime.Write(buf,0,buf.Length);
			
			buf = System.Text.Encoding.Default.GetBytes("--" + mainBoundaryID + "--\r\n");
			mime.Write(buf,0,buf.Length);

			mime.Position = 0;
			return mime;
		}

		#endregion

		#region method ConstructMime

		/// <summary>
		/// Constructs mime message.
		/// </summary>
		/// <returns></returns>
		public string ConstructMime()
		{
			return System.Text.Encoding.Default.GetString(ConstructBinaryMime().ToArray());

		/*	StringBuilder str = new StringBuilder();
			string mainBoundaryID = "----=_NextPart_" + Guid.NewGuid().ToString().Replace("-","_");

			str.Append("Message-ID: " + m_MsgID + "\r\n");
			str.Append("From: "       + CEnCode(m_From) + "\r\n");
			str.Append("To: "         + ConstructAddress(m_To));
			if(m_Cc != null && m_Cc.Length > 0){
				str.Append("Cc: "     + ConstructAddress(m_Cc));
			}
			if(m_Bcc != null && m_Bcc.Length > 0){
				str.Append("Bcc: "    + ConstructAddress(m_Bcc));
			}
			str.Append("Subject: "    + CEnCode(m_Subject) + "\r\n");
			str.Append("Date: "       + m_MsgDate.ToUniversalTime().ToString("r",System.Globalization.DateTimeFormatInfo.InvariantInfo) + "\r\n");
			str.Append("MIME-Version: 1.0\r\n");
			str.Append("Content-Type: " + "multipart/mixed;\r\n");
				str.Append("\tboundary=\"" + mainBoundaryID + "\"\r\n");

			str.Append("\r\nThis is a multi-part message in MIME format.\r\n\r\n");

			//----
			if(m_pAttachments.Count == 0){
				str.Append(ConstructBody(mainBoundaryID));
			}
			else{
				string bodyBoundaryID = "----=_NextPart_" + Guid.NewGuid().ToString().Replace("-","_");
				str.Append("--" + mainBoundaryID + "\r\n");
				str.Append("Content-Type: multipart/alternative;\r\n");
					str.Append("\tboundary=\"" + bodyBoundaryID + "\"\r\n\r\n");

				str.Append(ConstructBody(bodyBoundaryID));

				str.Append("--" + bodyBoundaryID + "--\r\n");

				//-- Construct attachments
				foreach(Attachment att in m_pAttachments){					
					str.Append("\r\n");
					str.Append("--" + mainBoundaryID + "\r\n");
					str.Append("Content-Type: application/octet;\r\n");
						str.Append("\tname=\"" + att.FileName + "\"\r\n");
					str.Append("Content-Transfer-Encoding: base64\r\n");
					str.Append("Content-Disposition: attachment;\r\n");
						str.Append("\tfilename=\"" + att.FileName + "\"\r\n\r\n");
				

					str.Append(SplitString(Convert.ToBase64String(att.FileData)));
				}

				str.Append("\r\n");
			}

			str.Append("--" + mainBoundaryID + "--\r\n");			
		
			return str.ToString();*/
		}

		#endregion


		#region function ConstructBody

		private string ConstructBody(string boundaryID)
		{
			StringBuilder str = new StringBuilder();

			str.Append("--" + boundaryID + "\r\n");
			str.Append("Content-Type: text/plain;\r\n");
				str.Append("\tcharset=\"" + m_CharSet + "\"\r\n");
			str.Append("Content-Transfer-Encoding: base64\r\n\r\n");

			str.Append(SplitString(Convert.ToBase64String(System.Text.Encoding.GetEncoding(m_CharSet).GetBytes(this.Body)) + "\r\n\r\n"));

			// We have html body, construct it.
			if(this.BodyHtml.Length > 0){
				str.Append("--" + boundaryID + "\r\n");
				str.Append("Content-Type: text/html;\r\n");
				str.Append("\tcharset=\"" + m_CharSet + "\"\r\n");
				str.Append("Content-Transfer-Encoding: base64\r\n\r\n");

				str.Append(SplitString(Convert.ToBase64String(System.Text.Encoding.GetEncoding(m_CharSet).GetBytes(this.BodyHtml))));
			}

			return str.ToString();
		}

		#endregion

		#region function ConstructAddress

		private string ConstructAddress(string[] address)
		{
			if(address != null && address.Length > 0){
				if(address.Length > 1){
					string to = "";
					for(int i=0;i<address.Length;i++){						
						if((address.Length - i) > 1){
							to += Core.CanonicalEncode(address[i],m_CharSet) + ",\r\n\t";
						}
						else{
							to += Core.CanonicalEncode(address[i],m_CharSet) + "\r\n";
						}
					}

					return to;
				}
				else{
					return Core.CanonicalEncode(address[0],m_CharSet) + "\r\n";
				}
			}

			return "";
		}

		#endregion


		#region function SplitString

		private string SplitString(string sIn)
		{
			StringBuilder str = new StringBuilder();

			int len = sIn.Length;
			int pos = 0;
			while(pos < len){
				if((len - pos) > 76){
					str.Append(sIn.Substring(pos,76) + "\r\n");
				}
				else{
					str.Append(sIn.Substring(pos,sIn.Length - pos) + "\r\n");
				}
				pos += 76;
			}

			return str.ToString();
		}

		#endregion

		#region method RemoveEmptyEntries

		private string[] RemoveEmptyEntries(string[] list)
		{
			ArrayList l = new ArrayList();
			foreach(string s in list){
				if(s.Length > 0){
					l.Add(s);
				}
			}

			string[] retVal = new string[l.Count];
			l.CopyTo(retVal);

			return retVal;
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Gets or sets mesaage ID.
		/// </summary>
		public string MessageID
		{
			get{ return m_MsgID; }

			set{ m_MsgID = value; }
		}

		/// <summary>
		/// Gets or sets receptients.
		/// </summary>
		public string[] To
		{
			get{ return m_To; }

			set{ m_To = RemoveEmptyEntries(value); }
		}

		/// <summary>
		/// Gets or sets .
		/// </summary>
		public string[] Cc
		{
			get{ return m_Cc; }

			set{ m_Cc = RemoveEmptyEntries(value); }
		}

		/// <summary>
		/// Gets or sets .
		/// </summary>
		public string[] Bcc
		{
			get{ return m_Bcc; }

			set{ m_Bcc = RemoveEmptyEntries(value); }
		}

		/// <summary>
		/// Gets or sets sender.
		/// </summary>
		public string From
		{
			get{ return m_From; }

			set{ m_From = value; }
		}

		/// <summary>
		/// Gets or sets subject.
		/// </summary>
		public string Subject
		{
			get{ return m_Subject; }

			set{ m_Subject = value; }
		}

		/// <summary>
		/// Gets or sets message date.
		/// </summary>
		public DateTime Date
		{
			get{ return m_MsgDate; }

			set{ m_MsgDate = value; }
		}

		/// <summary>
		/// Gets or sets body text.
		/// </summary>
		public string Body
		{
			get{ return m_Body; }

			set{ m_Body = value; }
		}

		/// <summary>
		/// Gets or sets html body.
		/// </summary>
		public string BodyHtml
		{
			get{ return m_BodyHtml; }

			set{ m_BodyHtml = value; }
		}

		/// <summary>
		/// Gets or sets message charset. Default is 'utf-8'.
		/// </summary>
		public string CharSet
		{
			get{ return m_CharSet; }

			set{ m_CharSet = value; }
		}

		/// <summary>
		/// Gets referance to attachments collection.
		/// </summary>
		public Attachments Attachments
		{
			get{ return m_pAttachments; }
		}

		#endregion

	}
}
